using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using Tailspin.CustomerCare.Domain;

namespace Tailspin.CustomerCare.Data.Repositories
{
    [Serializable]
    public class CaseSearchFilter
    {
        public string Text { get; set; }
        public int? QueueId { get; set; }
        public CaseStatus? Status { get; set; }
        public bool OpenOnly { get; set; }
        public string AssignedTo { get; set; }
        public bool BreachedOnly { get; set; }
        public string SortColumn { get; set; }
        public bool SortDescending { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; } = 25;
    }

    [Serializable]
    public class CaseListItem
    {
        public int Id { get; set; }
        public string CaseNumber { get; set; }
        public string Subject { get; set; }
        public string CustomerName { get; set; }
        public string AccountNumber { get; set; }
        public string QueueName { get; set; }
        public CasePriority Priority { get; set; }
        public CaseStatus Status { get; set; }
        public string AssignedTo { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime ResolveDueUtc { get; set; }
        public int EscalationLevel { get; set; }
    }

    public class PagedResult<T>
    {
        public IList<T> Items { get; set; }
        public int TotalRows { get; set; }
    }

    public class CaseRepository
    {
        private static readonly string[] SortColumns = { "CaseNumber", "Subject", "CustomerName", "QueueName", "Priority", "Status", "AssignedTo", "CreatedUtc", "ResolveDueUtc" };

        public PagedResult<CaseListItem> Search(CaseSearchFilter f)
        {
            var sort = SortColumns.Contains(f.SortColumn ?? string.Empty) ? f.SortColumn : "ResolveDueUtc";
            var total = new SqlParameter("@TotalRows", SqlDbType.Int) { Direction = ParameterDirection.Output };
            var items = new List<CaseListItem>();

            using (var cn = Db.Open())
            using (var cmd = Db.Proc(cn, "care.usp_Case_Search",
                Db.P("@Text", string.IsNullOrWhiteSpace(f.Text) ? null : f.Text.Trim()),
                Db.P("@QueueId", f.QueueId),
                Db.P("@Status", f.Status.HasValue ? (object)(int)f.Status.Value : null),
                Db.P("@OpenOnly", f.OpenOnly),
                Db.P("@AssignedTo", f.AssignedTo),
                Db.P("@BreachedOnly", f.BreachedOnly),
                Db.P("@SortColumn", sort),
                Db.P("@SortDesc", f.SortDescending),
                Db.P("@PageIndex", Math.Max(0, f.PageIndex)),
                Db.P("@PageSize", f.PageSize),
                total))
            {
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read()) items.Add(MapListItem(r));
                }

                return new PagedResult<CaseListItem> { Items = items, TotalRows = total.Value == DBNull.Value ? 0 : (int)total.Value };
            }
        }

        public Case Get(int id)
        {
            using (var db = new CareDbContext())
            {
                return db.Cases
                    .Include(c => c.Customer)
                    .Include(c => c.Queue)
                    .Include(c => c.Notes)
                    .AsNoTracking()
                    .FirstOrDefault(c => c.Id == id);
            }
        }

        public Case GetByNumber(string caseNumber)
        {
            using (var db = new CareDbContext())
            {
                return db.Cases.Include(c => c.Customer).Include(c => c.Queue).Include(c => c.Notes).AsNoTracking()
                    .FirstOrDefault(c => c.CaseNumber == caseNumber);
            }
        }

        public IList<CaseAttachment> ListAttachments(int caseId)
        {
            using (var db = new CareDbContext())
            {
                // Content is not loaded for the list.
                return db.CaseAttachments.Where(a => a.CaseId == caseId)
                    .OrderBy(a => a.UploadedUtc)
                    .Select(a => new { a.Id, a.CaseId, a.FileName, a.ContentType, a.Length, a.UploadedBy, a.UploadedUtc })
                    .ToList()
                    .Select(a => new CaseAttachment { Id = a.Id, CaseId = a.CaseId, FileName = a.FileName, ContentType = a.ContentType, Length = a.Length, UploadedBy = a.UploadedBy, UploadedUtc = a.UploadedUtc })
                    .ToList();
            }
        }

        public CaseAttachment GetAttachment(int id)
        {
            using (var db = new CareDbContext())
            {
                return db.CaseAttachments.AsNoTracking().FirstOrDefault(a => a.Id == id);
            }
        }

        public IList<Case> ListForCustomer(int customerId)
        {
            using (var db = new CareDbContext())
            {
                return db.Cases.Include(c => c.Queue).AsNoTracking().Where(c => c.CustomerId == customerId).OrderByDescending(c => c.CreatedUtc).ToList();
            }
        }

        public IList<Case> ListOpen()
        {
            using (var db = new CareDbContext())
            {
                return db.Cases.Include(c => c.Customer).Include(c => c.Queue).AsNoTracking()
                    .Where(c => c.Status != CaseStatus.Resolved && c.Status != CaseStatus.Closed)
                    .ToList();
            }
        }

        public string NextCaseNumber(int year)
        {
            var seq = Db.Scalar<int>("care.usp_Case_NextNumber", Db.P("@Year", year));
            return CaseNumber.Format(year, seq);
        }

        public void Insert(Case c)
        {
            using (var db = new CareDbContext())
            {
                db.Cases.Add(c);
                db.SaveChanges();
            }
        }

        /// <summary>Saves scalar changes with optimistic concurrency on RowVersion.</summary>
        public void Update(Case c)
        {
            using (var db = new CareDbContext())
            {
                var notes = c.Notes;
                var attachments = c.Attachments;
                var customer = c.Customer;
                var queue = c.Queue;
                c.Notes = new List<CaseNote>();
                c.Attachments = new List<CaseAttachment>();
                c.Customer = null;
                c.Queue = null;
                try
                {
                    db.Cases.Attach(c);
                    db.Entry(c).State = EntityState.Modified;
                    db.SaveChanges();
                }
                finally
                {
                    c.Notes = notes;
                    c.Attachments = attachments;
                    c.Customer = customer;
                    c.Queue = queue;
                }
            }
        }

        public void AddNote(CaseNote note)
        {
            using (var db = new CareDbContext())
            {
                db.CaseNotes.Add(note);
                db.SaveChanges();
            }
        }

        public void AddAttachment(CaseAttachment attachment)
        {
            using (var db = new CareDbContext())
            {
                db.CaseAttachments.Add(attachment);
                db.SaveChanges();
            }
        }

        public void Assign(int caseId, string assignee, string modifiedBy)
        {
            Db.Execute("care.usp_Case_Assign", Db.P("@CaseId", caseId), Db.P("@AssignedTo", assignee), Db.P("@ModifiedBy", modifiedBy));
        }

        public void SetEscalationLevel(int caseId, int level)
        {
            Db.Execute("care.usp_Case_SetEscalation", Db.P("@CaseId", caseId), Db.P("@Level", level));
        }

        internal static CaseListItem MapListItem(SqlDataReader r)
        {
            return new CaseListItem
            {
                Id = r.GetInt("Id"),
                CaseNumber = r.GetString("CaseNumber"),
                Subject = r.GetString("Subject"),
                CustomerName = r.GetString("CustomerName"),
                AccountNumber = r.GetString("AccountNumber"),
                QueueName = r.GetString("QueueName"),
                Priority = (CasePriority)r.GetInt("Priority"),
                Status = (CaseStatus)r.GetInt("Status"),
                AssignedTo = r.GetString("AssignedTo"),
                CreatedUtc = r.GetDate("CreatedUtc"),
                ResolveDueUtc = r.GetDate("ResolveDueUtc"),
                EscalationLevel = r.GetInt("EscalationLevel")
            };
        }
    }
}
