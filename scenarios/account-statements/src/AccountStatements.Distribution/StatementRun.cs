using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Woodgrove.Statements.Core.Delivery;
using Woodgrove.Statements.Core.Ledger;
using Woodgrove.Statements.Core.Model;
using Woodgrove.Statements.Core.Reports;
using Woodgrove.Statements.Core.Scheduling;
using Woodgrove.Statements.Data;
using Woodgrove.Statements.Distribution.Delivery;
using Woodgrove.Statements.Reporting;
using Woodgrove.Statements.Reporting.Pdf;

namespace Woodgrove.Statements.Distribution
{
    /// <summary>
    /// Monthly statement run: one statement per customer, delivered the way the customer wants it (e-mail, drop folder on the
    /// statement share, or a printed letter collected into the print pack for the print room).
    /// </summary>
    public class StatementRun
    {
        readonly DistributionSettings _settings;
        readonly CustomerRepository _customers;
        readonly DocumentService _documents;
        readonly DeliveryRepository _deliveries;
        readonly DocumentArchive _archive;
        readonly MailSender _mail;
        readonly RetryPolicy _retry = new RetryPolicy();

        public StatementRun(DistributionSettings settings, CustomerRepository customers, DocumentService documents, DeliveryRepository deliveries,
                            DocumentArchive archive, MailSender mail)
        {
            _settings = settings;
            _customers = customers;
            _documents = documents;
            _deliveries = deliveries;
            _archive = archive;
            _mail = mail;
        }

        public void Execute(Subscription s, ParameterSet parameters, int runId, DateTime runDate, RunOutcome outcome)
        {
            var statementDate = parameters.GetDate("StatementDate", runDate.Date.AddDays(-runDate.Day));
            var periodStart = parameters.GetDate("PeriodStart", new DateTime(statementDate.Year, statementDate.Month, 1));
            var minimum = decimal.Parse(string.IsNullOrWhiteSpace(parameters["MinimumBalance"]) ? "0" : parameters["MinimumBalance"], CultureInfo.InvariantCulture);
            var shareRoot = string.IsNullOrWhiteSpace(s.SharePath) ? _settings.StatementShareRoot : s.SharePath;
            var values = FileNameTemplate.Values(parameters.Items, runDate);
            var template = string.IsNullOrWhiteSpace(s.FileNameTemplate) ? "{CustomerNo}_{StatementDate:yyyyMMdd}.pdf" : s.FileNameTemplate;

            var print = new List<Tuple<PrintPackItem, DeliveryRecord>>();
            var skipped = 0;
            var counts = new Dictionary<string, int> { { DeliveryMethods.Email, 0 }, { DeliveryMethods.Share, 0 }, { DeliveryMethods.Print, 0 } };

            foreach (var customer in _customers.All().Where(c => c.Delivery != StatementDelivery.None))
            {
                try
                {
                    var statement = _documents.BuildStatement(customer, periodStart, statementDate);
                    if (!statement.HasActivity || (statement.Lines.Count == 0 && Math.Abs(statement.Aging.Total) < minimum))
                    {
                        skipped++;
                        continue;
                    }
                    var doc = _documents.CustomerStatement(statement, OutputFormat.Pdf);
                    values["CustomerNo"] = customer.CustomerNo;
                    values["StatementDate"] = statementDate;
                    var fileName = FileNameTemplate.Expand(template, values);
                    var archivePath = _archive.Store(runDate, runId, fileName, doc.Content);
                    outcome.Documents++;

                    var method = customer.Delivery;
                    if (method == StatementDelivery.Email && !RecipientList.IsValid(customer.Email))
                    {
                        outcome.Messages.Add(customer.CustomerNo + ": no valid e-mail address, statement printed instead.");
                        method = StatementDelivery.Post;
                    }
                    var record = new DeliveryRecord
                    {
                        RunId = runId, CustomerId = customer.CustomerId, DocumentName = fileName, ArchivePath = archivePath, Attempts = 1
                    };

                    if (method == StatementDelivery.Post)
                    {
                        record.Method = DeliveryMethods.Print;
                        print.Add(Tuple.Create(new PrintPackItem { CustomerNo = customer.CustomerNo, CustomerName = customer.Name, CountryCode = customer.CountryCode, Pdf = doc.Content }, record));
                        continue;
                    }

                    try
                    {
                        if (method == StatementDelivery.Email)
                        {
                            record.Method = DeliveryMethods.Email;
                            record.Target = customer.Email;
                            _mail.Send(RecipientList.Parse(customer.Email), MailTexts.StatementSubject(statement), MailTexts.StatementBody(statement),
                                       new[] { new MailAttachment { FileName = fileName, MimeType = doc.MimeType, Content = doc.Content } }, customerMail: true);
                        }
                        else
                        {
                            record.Method = DeliveryMethods.Share;
                            var folder = Path.Combine(shareRoot, "Customers", FileNameTemplate.Sanitize(string.IsNullOrWhiteSpace(customer.DropFolder) ? customer.CustomerNo : customer.DropFolder));
                            record.Target = ShareWriter.Write(folder, fileName, doc.Content);
                        }
                        record.Status = DeliveryStatus.Sent;
                        record.DeliveredAt = DateTime.Now;
                        counts[record.Method]++;
                    }
                    catch (Exception ex)
                    {
                        record.Target = record.Target ?? "";
                        record.LastError = ex.Message;
                        record.NextAttemptAt = _retry.NextAttempt(1, DateTime.Now);
                        record.Status = record.NextAttemptAt.HasValue ? DeliveryStatus.Retry : DeliveryStatus.Failed;
                        outcome.Failures++;
                        outcome.Messages.Add(customer.CustomerNo + ": " + ex.Message);
                        Log.Warn("Statement delivery for {0} failed: {1}", customer.CustomerNo, ex.Message);
                    }
                    _deliveries.Add(record);
                }
                catch (Exception ex)
                {
                    outcome.Failures++;
                    outcome.Messages.Add(customer.CustomerNo + ": " + ex.Message);
                    Log.Error(ex, "Statement for {0} could not be produced", customer.CustomerNo);
                }
            }

            if (print.Count > 0)
                WritePrintPack(print, shareRoot, runId, runDate, statementDate, outcome, counts);

            var summary = string.Format(CultureInfo.InvariantCulture, "Statements {0:dd.MM.yyyy}: {1} produced, {2} e-mailed, {3} to drop folders, {4} printed, {5} without activity, {6} failures.",
                                        statementDate, outcome.Documents, counts[DeliveryMethods.Email], counts[DeliveryMethods.Share], counts[DeliveryMethods.Print], skipped, outcome.Failures);
            outcome.Messages.Insert(0, summary);
            if (!string.IsNullOrWhiteSpace(s.Recipients))
            {
                try
                {
                    _mail.Send(RecipientList.Parse(s.Recipients), "Statement run " + statementDate.ToString("dd.MM.yyyy") + " finished",
                               string.Join("\r\n", outcome.Messages) + "\r\n\r\nDetails: https://statements.corp.local/Subscriptions/Run/" + runId, null, customerMail: false);
                }
                catch (Exception ex)
                {
                    Log.Warn("Run summary mail failed: {0}", ex.Message);
                }
            }
        }

        void WritePrintPack(List<Tuple<PrintPackItem, DeliveryRecord>> print, string shareRoot, int runId, DateTime runDate, DateTime statementDate,
                            RunOutcome outcome, Dictionary<string, int> counts)
        {
            var folder = Path.Combine(shareRoot, "Print", statementDate.ToString("yyyyMM"));
            var baseName = "StatementPack_" + statementDate.ToString("yyyyMMdd") + "_run" + runId.ToString("000000");
            string packPath = null;
            string error = null;
            try
            {
                var builder = new PrintPackBuilder { Duplex = _settings.PrintDuplex };
                var pack = builder.Build(print.Select(p => p.Item1), "Account statements " + statementDate.ToString("dd.MM.yyyy"), runDate);
                packPath = ShareWriter.Write(folder, baseName + ".pdf", pack.Pdf);
                ShareWriter.Write(folder, baseName + ".csv", Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(pack.Manifest)).ToArray());
                _archive.Store(runDate, runId, baseName + ".pdf", pack.Pdf);
                outcome.Messages.Add(string.Format("Print pack {0}: {1} letters, {2} sheets.", packPath, pack.Documents, pack.Sheets));
                if (!string.IsNullOrWhiteSpace(_settings.PrintRoomNotify))
                    _mail.Send(RecipientList.Parse(_settings.PrintRoomNotify), "Statement print pack " + statementDate.ToString("dd.MM.yyyy") + " ready",
                               "Print pack: " + packPath + "\r\nManifest: " + Path.ChangeExtension(packPath, ".csv") + "\r\nLetters: " + pack.Documents + ", sheets: " + pack.Sheets +
                               (_settings.PrintDuplex ? " (duplex)" : ""), null, customerMail: false);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                outcome.Failures++;
                outcome.Messages.Add("Print pack: " + ex.Message);
                Log.Error(ex, "Print pack for run {0} failed", runId);
            }
            foreach (var p in print)
            {
                var record = p.Item2;
                record.Target = packPath ?? folder;
                record.Status = error == null ? DeliveryStatus.Sent : DeliveryStatus.Failed;
                record.LastError = error;
                record.DeliveredAt = error == null ? DateTime.Now : (DateTime?)null;
                _deliveries.Add(record);
                if (error == null) counts[DeliveryMethods.Print]++;
            }
        }
    }
}
