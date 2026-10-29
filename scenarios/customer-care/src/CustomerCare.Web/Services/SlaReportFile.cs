using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Tailspin.CustomerCare.Data;
using Tailspin.CustomerCare.Data.Repositories;
using Tailspin.CustomerCare.Domain;

namespace Tailspin.CustomerCare.Web.Services
{
    /// <summary>
    /// Writes the SLA report as a CSV file (Excel-friendly: UTF-8 with BOM, semicolon separated).
    /// The same report requested again within ten minutes is served from the file already written.
    /// </summary>
    public static class SlaReportFile
    {
        private static readonly TimeSpan Reuse = TimeSpan.FromMinutes(10);

        public static string Create(DateTime fromLocal, DateTime toLocal, int? queueId, Func<IList<SlaReportRow>> load)
        {
            var name = string.Format(CultureInfo.InvariantCulture, "sla_{0:yyyyMMdd}_{1:yyyyMMdd}_{2}_{3}.csv", fromLocal, toLocal, queueId.HasValue ? queueId.Value.ToString(CultureInfo.InvariantCulture) : "all", Hash(fromLocal, toLocal, queueId));
            var path = Path.Combine(WorkFolders.Reports, name);

            if (File.Exists(path) && File.GetLastWriteTimeUtc(path) > DateTime.UtcNow - Reuse)
            {
                return path;
            }

            var policy = ReferenceData.SlaPolicy;
            var cal = ReferenceData.Calendar;
            var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (var w = new StreamWriter(tmp, false, new UTF8Encoding(true)))
            {
                w.WriteLine("Case;Queue;Category;Priority;Status;Assigned to;Created;Response due;First response;Resolve due;Resolved;Response met;Resolution met");
                foreach (var r in load())
                {
                    var c = new Case
                    {
                        Status = r.Status,
                        CreatedUtc = r.CreatedUtc,
                        ResponseDueUtc = r.ResponseDueUtc,
                        ResolveDueUtc = r.ResolveDueUtc,
                        FirstResponseUtc = r.FirstResponseUtc,
                        ResolvedUtc = r.ResolvedUtc
                    };
                    var state = policy.Evaluate(c, DateTime.UtcNow);
                    var responseMet = r.FirstResponseUtc.HasValue ? (r.FirstResponseUtc <= r.ResponseDueUtc ? "yes" : "no") : (DateTime.UtcNow > r.ResponseDueUtc ? "no" : "");

                    w.WriteLine(string.Join(";",
                        r.CaseNumber, Csv(r.QueueName), r.CategoryCode, r.Priority, CaseWorkflow.DisplayName(r.Status), Csv(r.AssignedTo),
                        Local(cal, r.CreatedUtc), Local(cal, r.ResponseDueUtc), Local(cal, r.FirstResponseUtc), Local(cal, r.ResolveDueUtc), Local(cal, r.ResolvedUtc),
                        responseMet, state == SlaState.Met ? "yes" : state == SlaState.Breached ? "no" : ""));
                }
            }

            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            return path;
        }

        private static string Local(BusinessCalendar cal, DateTime? utc)
        {
            return utc.HasValue ? cal.ToLocal(utc.Value).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) : string.Empty;
        }

        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.IndexOfAny(new[] { ';', '"', '\n' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }

        private static string Hash(DateTime from, DateTime to, int? queue)
        {
            using (var sha = SHA1.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(from.Ticks + "|" + to.Ticks + "|" + queue));
                return BitConverter.ToString(bytes, 0, 4).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
