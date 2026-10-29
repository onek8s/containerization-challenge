using System;
using System.Data.Entity.Infrastructure;
using Tailspin.CustomerCare.Data.Repositories;
using Tailspin.CustomerCare.Domain;

namespace Tailspin.CustomerCare.Data.Services
{
    public class NewCaseRequest
    {
        public int CustomerId { get; set; }
        public string CategoryCode { get; set; }
        public string Subject { get; set; }
        public string Description { get; set; }
        public CasePriority Priority { get; set; } = CasePriority.Normal;
        public CaseChannel Channel { get; set; }
        public string ExternalReference { get; set; }
        /// <summary>Optional manual queue selection; routing rules are used when empty.</summary>
        public string QueueCode { get; set; }
        public bool AssignToMe { get; set; }
    }

    public class CaseChangedEventArgs : EventArgs
    {
        public CaseChangedEventArgs(Case c, string change)
        {
            Case = c;
            Change = change;
        }

        public Case Case { get; }
        public string Change { get; }
    }

    public class ConcurrencyConflictException : Exception
    {
        public ConcurrencyConflictException(string caseNumber, Exception inner)
            : base("Case " + caseNumber + " was changed by someone else. Reload the case and try again.", inner)
        {
        }
    }

    /// <summary>Case operations shared by the portal, the integration service and the background jobs.</summary>
    public class CaseService
    {
        private readonly CaseRepository _cases;
        private readonly CustomerRepository _customers;

        public CaseService()
            : this(new CaseRepository(), new CustomerRepository())
        {
        }

        public CaseService(CaseRepository cases, CustomerRepository customers)
        {
            _cases = cases;
            _customers = customers;
        }

        /// <summary>Raised after a case was created or changed. The host application pushes the change to listeners.</summary>
        public static event EventHandler<CaseChangedEventArgs> CaseChanged;

        public Case Create(NewCaseRequest request, string user)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.Subject)) throw new ArgumentException("Subject is required.");

            var customer = _customers.Get(request.CustomerId);
            if (customer == null) throw new ArgumentException("Customer " + request.CustomerId + " does not exist.");

            var queueCode = string.IsNullOrEmpty(request.QueueCode) ? ReferenceData.Router.Route(request.CategoryCode, customer) : request.QueueCode;
            var queue = ReferenceData.QueueByCode(queueCode) ?? ReferenceData.QueueByCode(CareSettings.DefaultQueue);
            if (queue == null) throw new InvalidOperationException("Queue '" + queueCode + "' is not configured.");

            var now = DateTime.UtcNow;
            var priority = CaseRouter.InitialPriority(request.Priority, customer);
            var due = ReferenceData.SlaPolicy.Calculate(now, priority, customer.IsPriorityCustomer);

            var c = new Case
            {
                CaseNumber = _cases.NextCaseNumber(ReferenceData.Calendar.ToLocal(now).Year),
                CustomerId = customer.Id,
                QueueId = queue.Id,
                CategoryCode = request.CategoryCode,
                Subject = request.Subject.Trim(),
                Description = request.Description,
                Priority = priority,
                Status = CaseStatus.New,
                Channel = request.Channel,
                ExternalReference = request.ExternalReference,
                CreatedUtc = now,
                CreatedBy = user,
                ModifiedUtc = now,
                ResponseDueUtc = due.ResponseDueUtc,
                ResolveDueUtc = due.ResolveDueUtc
            };

            if (request.AssignToMe)
            {
                CaseWorkflow.Move(c, CaseStatus.Assigned, user, now);
            }

            _cases.Insert(c);
            c.Customer = customer;
            c.Queue = queue;
            Raise(c, "created");
            return c;
        }

        public Case ChangeStatus(int caseId, byte[] rowVersion, CaseStatus status, string user)
        {
            var c = Load(caseId);
            c.RowVersion = rowVersion;
            var now = DateTime.UtcNow;
            CaseWorkflow.Move(c, status, user, now);
            if (status != CaseStatus.New) CaseWorkflow.RecordResponse(c, now);
            Save(c);
            Raise(c, "status");
            return c;
        }

        public Case Update(int caseId, byte[] rowVersion, string subject, string categoryCode, CasePriority priority, int queueId, string user)
        {
            var c = Load(caseId);
            c.RowVersion = rowVersion;
            var recalc = c.Priority != priority;

            c.Subject = subject;
            c.CategoryCode = categoryCode;
            c.Priority = priority;
            c.QueueId = queueId;
            c.ModifiedUtc = DateTime.UtcNow;

            if (recalc)
            {
                var due = ReferenceData.SlaPolicy.Calculate(c.CreatedUtc, priority, c.Customer != null && c.Customer.IsPriorityCustomer);
                c.ResponseDueUtc = due.ResponseDueUtc;
                c.ResolveDueUtc = due.ResolveDueUtc;
            }

            Save(c);
            Raise(c, "updated");
            return c;
        }

        public void Assign(int caseId, string assignee, string user)
        {
            _cases.Assign(caseId, assignee, user);
            Raise(_cases.Get(caseId), "assigned");
        }

        public CaseNote AddNote(int caseId, string body, bool isInternal, string user)
        {
            if (string.IsNullOrWhiteSpace(body)) throw new ArgumentException("Note text is required.");

            var now = DateTime.UtcNow;
            var note = new CaseNote { CaseId = caseId, Author = user, Body = body.Trim(), IsInternal = isInternal, CreatedUtc = now };
            _cases.AddNote(note);

            var c = Load(caseId);
            if (!isInternal && c.FirstResponseUtc == null)
            {
                CaseWorkflow.RecordResponse(c, now);
                c.ModifiedUtc = now;
                Save(c);
            }

            Raise(c, "note");
            return note;
        }

        public CaseAttachment AddAttachment(int caseId, string fileName, string contentType, byte[] content, string user)
        {
            if (content == null || content.Length == 0) throw new ArgumentException("The file is empty.");
            if (content.Length > CareSettings.MaxAttachmentBytes) throw new ArgumentException("The file is larger than the allowed " + CareSettings.MaxAttachmentBytes / 1024 + " KB.");

            var a = new CaseAttachment
            {
                CaseId = caseId,
                FileName = System.IO.Path.GetFileName(fileName),
                ContentType = string.IsNullOrEmpty(contentType) ? "application/octet-stream" : contentType,
                Content = content,
                Length = content.Length,
                UploadedBy = user,
                UploadedUtc = DateTime.UtcNow
            };
            _cases.AddAttachment(a);
            return a;
        }

        private Case Load(int caseId)
        {
            var c = _cases.Get(caseId);
            if (c == null) throw new ArgumentException("Case " + caseId + " does not exist.");
            return c;
        }

        private void Save(Case c)
        {
            try
            {
                _cases.Update(c);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new ConcurrencyConflictException(c.CaseNumber, ex);
            }
        }

        private static void Raise(Case c, string change)
        {
            var handler = CaseChanged;
            if (handler != null && c != null) handler(null, new CaseChangedEventArgs(c, change));
        }
    }
}
