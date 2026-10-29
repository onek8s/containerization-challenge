using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Northwind.Inspections.Core;
using Northwind.Inspections.Infrastructure.Configuration;
using Northwind.Inspections.Infrastructure.Data;
using Northwind.Inspections.Infrastructure.Diagnostics;
using Northwind.Inspections.Infrastructure.Imaging;

namespace Northwind.Inspections.Jobs
{
    internal sealed class ReportJob
    {
        private readonly InspectionSettings settings = InspectionSettings.Current;
        private readonly Db db;
        private readonly ReportQueue queue;
        private readonly InspectionRepository inspections;
        private readonly PhotoRepository photos;
        private readonly PhotoStore store;

        public ReportJob()
        {
            db = new Db(settings.ConnectionString);
            queue = new ReportQueue(db);
            inspections = new InspectionRepository(db);
            photos = new PhotoRepository(db);
            store = new PhotoStore(settings, db);
        }

        public int Run()
        {
            var worker = Environment.MachineName + "/" + Process.GetCurrentProcess().Id;
            var sent = 0;
            var failed = 0;

            for (var n = 0; n < settings.ReportBatchSize; n++)
            {
                var request = queue.ClaimNext(worker);
                if (request == null)
                {
                    break;
                }

                try
                {
                    Send(request);
                    queue.Complete(request.Id);
                    inspections.SetStatus(request.InspectionId, InspectionStatus.ReportSent, "Inspections.Jobs");
                    InspectionsCounters.Instance.ReportSent();
                    sent++;
                }
                catch (Exception ex)
                {
                    failed++;
                    InspectionsCounters.Instance.ReportFailed();
                    var retry = queue.Fail(request, ex.GetBaseException().Message);
                    if (retry)
                    {
                        InspectionsLog.Warning(EventId.ReportRetry, "Report {0} (attempt {1}) failed and will be retried: {2}", request.Id, request.Attempts, ex);
                    }
                    else
                    {
                        inspections.SetStatus(request.InspectionId, InspectionStatus.Submitted, "Inspections.Jobs");
                        InspectionsLog.Error(EventId.ReportAbandoned, ex, "Report {0} for inspection {1} abandoned after {2} attempts.", request.Id, request.InspectionId, request.Attempts);
                    }
                }
            }

            InspectionsCounters.Instance.QueueDepth(queue.Depth());
            Trace.TraceInformation("run-reports: {0} sent, {1} failed.", sent, failed);
            return failed == 0 ? 0 : 3;
        }

        private void Send(ReportRequest request)
        {
            var inspection = inspections.Get(request.InspectionId);
            if (inspection == null)
            {
                throw new InvalidOperationException("Inspection " + request.InspectionId + " no longer exists.");
            }
            var list = photos.ForInspection(inspection.Id).Where(p => !p.Purged).ToList();
            var thumbs = list.Select(p => new KeyValuePair<Photo, byte[]>(p, store.Read(p, PhotoSize.Thumbnail))).ToList();
            var sheet = ContactSheet.Render(inspection, thumbs, settings.WatermarkFont);

            var report = new InspectionReport
            {
                Inspection = inspection,
                Photos = list,
                ContactSheetContentId = "contact-sheet",
                GeneratedUtc = DateTime.UtcNow
            };
            var html = ReportBuilder.Html(report, settings.PortalUrl);

            using (var message = new MailMessage())
            using (var sheetStream = new MemoryStream(sheet))
            {
                message.From = new MailAddress(settings.SmtpFrom, "Northwind Property Inspections");
                message.To.Add(request.Recipient);
                var requester = new Infrastructure.Data.AdjusterRepository(db).FindByLogin(request.RequestedBy);
                if (requester != null && !string.IsNullOrEmpty(requester.Email))
                {
                    message.CC.Add(requester.Email);
                }
                message.Subject = ReportBuilder.Subject(inspection);
                message.SubjectEncoding = Encoding.UTF8;
                message.Headers.Add("X-Inspection-Report", request.Id.ToString());

                var view = AlternateView.CreateAlternateViewFromString(html, Encoding.UTF8, MediaTypeNames.Text.Html);
                view.LinkedResources.Add(new LinkedResource(sheetStream, MediaTypeNames.Image.Jpeg) { ContentId = report.ContactSheetContentId });
                message.AlternateViews.Add(view);

                using (var smtp = new SmtpClient(settings.SmtpHost, settings.SmtpPort))
                {
                    if (string.IsNullOrEmpty(settings.SmtpUser))
                    {
                        smtp.UseDefaultCredentials = true;
                    }
                    else
                    {
                        smtp.Credentials = new NetworkCredential(settings.SmtpUser, settings.SmtpPassword);
                        smtp.EnableSsl = settings.SmtpPort == 587;
                    }
                    smtp.Send(message);
                }
            }

            store.Share.Run(() =>
            {
                var folder = Path.GetDirectoryName(PhotoStorage.Combine(settings.PhotoShare, PhotoStorage.RelativePath(inspection.ClaimNumber, Guid.Empty)));
                File.WriteAllText(Path.Combine(folder, "report-" + request.Id + ".html"), html.Replace("cid:contact-sheet", "report-" + request.Id + ".jpg"), Encoding.UTF8);
                File.WriteAllBytes(Path.Combine(folder, "report-" + request.Id + ".jpg"), sheet);
            });

            InspectionsLog.Info(EventId.ReportSent, "Report {0} for {1} sent to {2} with {3} photos.", request.Id, inspection.ClaimNumber, request.Recipient, list.Count);
        }
    }
}
