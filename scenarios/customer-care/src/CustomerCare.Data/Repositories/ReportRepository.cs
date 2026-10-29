using System;
using System.Collections.Generic;
using Tailspin.CustomerCare.Domain;

namespace Tailspin.CustomerCare.Data.Repositories
{
    public class SlaReportRow
    {
        public string CaseNumber { get; set; }
        public string QueueName { get; set; }
        public string CategoryCode { get; set; }
        public CasePriority Priority { get; set; }
        public CaseStatus Status { get; set; }
        public string AssignedTo { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime ResponseDueUtc { get; set; }
        public DateTime? FirstResponseUtc { get; set; }
        public DateTime ResolveDueUtc { get; set; }
        public DateTime? ResolvedUtc { get; set; }
    }

    public class AgentWorkload
    {
        public string AssignedTo { get; set; }
        public int Open { get; set; }
        public int Breached { get; set; }
        public int ResolvedLast7Days { get; set; }
    }

    public class SearchHit
    {
        public string Kind { get; set; }
        public int Id { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
    }

    public class ReportRepository
    {
        public IList<QueueStats> QueueStats(DateTime dayStartUtc)
        {
            return Db.Query("care.usp_Dashboard_QueueStats", r => new QueueStats
            {
                QueueCode = r.GetString("QueueCode"),
                QueueName = r.GetString("QueueName"),
                Open = r.GetInt("OpenCases"),
                Unassigned = r.GetInt("Unassigned"),
                AtRisk = r.GetInt("AtRisk"),
                Breached = r.GetInt("Breached"),
                CreatedToday = r.GetInt("CreatedToday"),
                ResolvedToday = r.GetInt("ResolvedToday"),
                AverageResolveHours = r.GetNullableDouble("AvgResolveHours")
            }, Db.P("@DayStartUtc", dayStartUtc), Db.P("@NowUtc", DateTime.UtcNow));
        }

        public IList<AgentWorkload> AgentWorkload()
        {
            return Db.Query("care.usp_Dashboard_AgentWorkload", r => new AgentWorkload
            {
                AssignedTo = r.GetString("AssignedTo"),
                Open = r.GetInt("OpenCases"),
                Breached = r.GetInt("Breached"),
                ResolvedLast7Days = r.GetInt("ResolvedLast7Days")
            }, Db.P("@NowUtc", DateTime.UtcNow));
        }

        public IList<SlaReportRow> Sla(DateTime fromUtc, DateTime toUtc, int? queueId)
        {
            return Db.Query("care.usp_Report_Sla", r => new SlaReportRow
            {
                CaseNumber = r.GetString("CaseNumber"),
                QueueName = r.GetString("QueueName"),
                CategoryCode = r.GetString("CategoryCode"),
                Priority = (CasePriority)r.GetInt("Priority"),
                Status = (CaseStatus)r.GetInt("Status"),
                AssignedTo = r.GetString("AssignedTo"),
                CreatedUtc = r.GetDate("CreatedUtc"),
                ResponseDueUtc = r.GetDate("ResponseDueUtc"),
                FirstResponseUtc = r.GetNullableDate("FirstResponseUtc"),
                ResolveDueUtc = r.GetDate("ResolveDueUtc"),
                ResolvedUtc = r.GetNullableDate("ResolvedUtc")
            }, Db.P("@FromUtc", fromUtc), Db.P("@ToUtc", toUtc), Db.P("@QueueId", queueId));
        }

        public IList<SearchHit> GlobalSearch(string text)
        {
            return Db.Query("care.usp_Search_Global", r => new SearchHit
            {
                Kind = r.GetString("Kind"),
                Id = r.GetInt("Id"),
                Title = r.GetString("Title"),
                Detail = r.GetString("Detail")
            }, Db.P("@Text", text));
        }

        public void SaveDailyStats(DateTime day, IEnumerable<QueueStats> stats)
        {
            foreach (var s in stats)
            {
                Db.Execute("care.usp_Stats_SaveDaily",
                    Db.P("@Day", day.Date),
                    Db.P("@QueueCode", s.QueueCode),
                    Db.P("@OpenCases", s.Open),
                    Db.P("@Breached", s.Breached),
                    Db.P("@Created", s.CreatedToday),
                    Db.P("@Resolved", s.ResolvedToday),
                    Db.P("@AvgResolveHours", s.AverageResolveHours));
            }
        }
    }
}
