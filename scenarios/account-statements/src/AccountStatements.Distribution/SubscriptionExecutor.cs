using System;
using System.Collections.Generic;
using System.Linq;
using Woodgrove.Statements.Core.Delivery;
using Woodgrove.Statements.Core.Model;
using Woodgrove.Statements.Core.Reports;
using Woodgrove.Statements.Core.Scheduling;
using Woodgrove.Statements.Data;
using Woodgrove.Statements.Distribution.Delivery;
using Woodgrove.Statements.Reporting;

namespace Woodgrove.Statements.Distribution
{
    public class RunOutcome
    {
        public int Documents { get; set; }
        public int Failures { get; set; }
        public List<string> Messages { get; } = new List<string>();

        public string Status
        {
            get { return Failures == 0 ? RunStatus.Succeeded : Documents > Failures ? RunStatus.Partial : RunStatus.Failed; }
        }
    }

    /// <summary>Runs one subscription: renders, archives and delivers, and records the run and every delivery.</summary>
    public class SubscriptionExecutor
    {
        readonly DistributionSettings _settings;
        readonly SubscriptionRepository _subscriptions;
        readonly DeliveryRepository _deliveries;
        readonly DocumentService _documents;
        readonly DocumentArchive _archive;
        readonly MailSender _mail;
        readonly StatementRun _statementRun;
        readonly RetryPolicy _retry = new RetryPolicy();

        public SubscriptionExecutor(DistributionSettings settings, Db db)
        {
            _settings = settings;
            _subscriptions = new SubscriptionRepository(db);
            _deliveries = new DeliveryRepository(db);
            _documents = new DocumentService(db);
            _archive = new DocumentArchive(settings.ArchiveRoot);
            _mail = new MailSender(settings);
            _statementRun = new StatementRun(settings, new CustomerRepository(db), _documents, _deliveries, _archive, _mail);
        }

        public RunOutcome Execute(Subscription subscription, string runBy, DateTime runDate)
        {
            var runId = _subscriptions.StartRun(subscription.SubscriptionId, subscription.NextRunAt, runBy, _settings.HostName);
            var outcome = new RunOutcome();
            Log.Info("Run {0} started: subscription {1} '{2}' ({3}) by {4}", runId, subscription.SubscriptionId, subscription.Name, subscription.ReportCode, runBy);
            try
            {
                var parameters = ParameterSet.Parse(subscription.Parameters).Resolve(runDate);
                if (subscription.ReportCode == ReportCodes.StatementRun)
                    _statementRun.Execute(subscription, parameters, runId, runDate, outcome);
                else
                    SingleDocument(subscription, parameters, runId, runDate, outcome);
            }
            catch (Exception ex)
            {
                outcome.Failures++;
                outcome.Messages.Add(ex.Message);
                Log.Error(ex, "Run {0} of subscription {1} failed", runId, subscription.SubscriptionId);
            }
            _subscriptions.FinishRun(runId, outcome.Status, outcome.Documents, outcome.Failures, string.Join(Environment.NewLine, outcome.Messages.Take(20)));
            Log.Info("Run {0} finished: {1}, {2} documents, {3} failures", runId, outcome.Status, outcome.Documents, outcome.Failures);
            return outcome;
        }

        void SingleDocument(Subscription s, ParameterSet parameters, int runId, DateTime runDate, RunOutcome outcome)
        {
            var format = OutputFormats.Parse(s.OutputFormat);
            var doc = _documents.Render(s.ReportCode, parameters, format, runDate);
            var values = FileNameTemplate.Values(parameters.Items, runDate);
            var fileName = string.IsNullOrWhiteSpace(s.FileNameTemplate) ? doc.FileName : FileNameTemplate.Expand(s.FileNameTemplate, values);
            if (!fileName.EndsWith(OutputFormats.Extension(format), StringComparison.OrdinalIgnoreCase)) fileName += OutputFormats.Extension(format);
            var archivePath = _archive.Store(runDate, runId, fileName, doc.Content);
            outcome.Documents++;

            var record = new DeliveryRecord { RunId = runId, Method = s.DeliveryMethod, DocumentName = fileName, ArchivePath = archivePath, Attempts = 1 };
            try
            {
                if (s.DeliveryMethod == DeliveryMethods.Email)
                {
                    record.Target = s.Recipients ?? "";
                    var subject = FileNameTemplate.ExpandText(string.IsNullOrWhiteSpace(s.EmailSubject) ? s.Name : s.EmailSubject, values);
                    _mail.Send(RecipientList.Parse(s.Recipients), subject, MailTexts.ReportBody(s.Name, fileName, runDate),
                               new[] { new MailAttachment { FileName = fileName, MimeType = doc.MimeType, Content = doc.Content } }, customerMail: false);
                }
                else if (s.DeliveryMethod == DeliveryMethods.Share)
                {
                    record.Target = ShareWriter.Write(s.SharePath, fileName, doc.Content);
                }
                else
                {
                    throw new InvalidOperationException("Delivery method " + s.DeliveryMethod + " is only for statement runs.");
                }
                record.Status = DeliveryStatus.Sent;
                record.DeliveredAt = DateTime.Now;
            }
            catch (Exception ex)
            {
                if (string.IsNullOrEmpty(record.Target)) record.Target = s.SharePath ?? s.Recipients ?? "";
                record.LastError = ex.Message;
                record.NextAttemptAt = _retry.NextAttempt(1, DateTime.Now);
                record.Status = record.NextAttemptAt.HasValue ? DeliveryStatus.Retry : DeliveryStatus.Failed;
                outcome.Failures++;
                outcome.Messages.Add(fileName + ": " + ex.Message);
                Log.Warn("Delivery of {0} ({1}) failed: {2}", fileName, s.DeliveryMethod, ex.Message);
            }
            _deliveries.Add(record);
        }

        /// <summary>Resends failed e-mails and share copies from the archive copy.</summary>
        public int ProcessRetries(DateTime now)
        {
            var count = 0;
            foreach (var d in _deliveries.DueRetries(now))
            {
                var attempt = d.Attempts + 1;
                try
                {
                    var content = _archive.Load(d.ArchivePath);
                    var format = d.DocumentName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ? OutputFormat.Excel
                               : d.DocumentName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) ? OutputFormat.Word : OutputFormat.Pdf;
                    if (d.Method == DeliveryMethods.Email)
                    {
                        var subject = d.CustomerId.HasValue ? "Account statement - Woodgrove Leasing" : (d.SubscriptionName ?? d.DocumentName);
                        _mail.Send(RecipientList.Parse(d.Target), subject, MailTexts.ReportBody(d.SubscriptionName, d.DocumentName, now),
                                   new[] { new MailAttachment { FileName = d.DocumentName, MimeType = OutputFormats.MimeType(format), Content = content } },
                                   customerMail: d.CustomerId.HasValue);
                    }
                    else if (d.Method == DeliveryMethods.Share)
                    {
                        var folder = d.Target.EndsWith(d.DocumentName, StringComparison.OrdinalIgnoreCase) ? System.IO.Path.GetDirectoryName(d.Target) : d.Target;
                        ShareWriter.Write(folder, d.DocumentName, content);
                    }
                    else
                    {
                        continue;
                    }
                    _deliveries.Update(d.DeliveryId, DeliveryStatus.Sent, attempt, null, null);
                    Log.Info("Delivery {0} ({1} to {2}) sent on attempt {3}", d.DeliveryId, d.DocumentName, d.Target, attempt);
                    count++;
                }
                catch (Exception ex)
                {
                    var next = _retry.NextAttempt(attempt, now);
                    _deliveries.Update(d.DeliveryId, next.HasValue ? DeliveryStatus.Retry : DeliveryStatus.Failed, attempt, next, ex.Message);
                    Log.Warn("Delivery {0} attempt {1} failed: {2}", d.DeliveryId, attempt, ex.Message);
                }
            }
            return count;
        }
    }
}
