using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Contractors;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Documents;
using Fabrikam.Housing.Finance;
using Fabrikam.Housing.Integration;
using Fabrikam.Housing.Integration.Bacs;
using Fabrikam.Housing.Integration.Messaging;
using Fabrikam.Housing.Jobs.Infrastructure;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fabrikam.Housing.Jobs.Jobs
{
    /// <summary>
    /// Weekly contractor payment run: approved invoices are paid by Bacs through the payment bureau.
    /// </summary>
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(1800)]
    public class ContractorPaymentRunJob : IHousingJob
    {
        private readonly HousingDbContext _db;
        private readonly CounterService _counters;
        private readonly IBacsBureau _bureau;
        private readonly ILetterComposer _letters;
        private readonly IPdfRenderer _pdf;
        private readonly TenantMessenger _messenger;
        private readonly WorkingCalendar _calendar;
        private readonly IClock _clock;
        private readonly BacsBureauOptions _bacs;
        private readonly JobsOptions _options;
        private readonly ILogger<ContractorPaymentRunJob> _logger;

        public ContractorPaymentRunJob(HousingDbContext db, CounterService counters, IBacsBureau bureau, ILetterComposer letters, IPdfRenderer pdf,
            TenantMessenger messenger, WorkingCalendar calendar, IClock clock, IOptions<IntegrationOptions> integration, IOptions<JobsOptions> options,
            ILogger<ContractorPaymentRunJob> logger)
        {
            _db = db;
            _counters = counters;
            _bureau = bureau;
            _letters = letters;
            _pdf = pdf;
            _messenger = messenger;
            _calendar = calendar;
            _clock = clock;
            _bacs = integration.Value.BacsBureau;
            _options = options.Value;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            var invoices = await _db.ContractorInvoices.Include(i => i.Contractor).Include(i => i.WorkOrder)
                .Where(i => i.Status == InvoiceStatus.Approved && i.PaymentRunId == null)
                .ToListAsync();
            if (invoices.Count == 0)
            {
                _logger.LogInformation("Contractor payment run: no approved invoices");
                return;
            }

            var payable = invoices.Where(i => HasBankDetails(i.Contractor)).GroupBy(i => i.Contractor).ToList();
            var skipped = invoices.Where(i => !HasBankDetails(i.Contractor)).Select(i => i.Contractor.Code).Distinct().ToList();
            if (payable.Count == 0)
            {
                _logger.LogWarning("Contractor payment run: no contractor has bank details ({Skipped})", string.Join(", ", skipped));
                return;
            }

            var fileNumber = await _counters.NextAsync("BacsFile");
            // The bureau needs the file the working day before Bacs input day; credits arrive two days later.
            var processingDate = _calendar.AddWorkingDays(_clock.Today, 3).Date;
            var runReference = "FAB" + fileNumber.ToString("000000");
            var payments = payable.Select(g => new BacsPayment
            {
                DestinationSortCode = g.Key.BankSortCode,
                DestinationAccount = g.Key.BankAccountNumber,
                DestinationName = g.Key.BankAccountName ?? g.Key.Name,
                Reference = "FABRIKAM " + runReference,
                Amount = Money.Round(g.Sum(i => i.GrossAmount))
            }).ToList();

            var writer = new BacsStandard18
            {
                ServiceUserNumber = _bacs.ServiceUserNumber,
                OriginSortCode = _bacs.OriginSortCode,
                OriginAccount = _bacs.OriginAccount
            };
            var content = writer.Build(payments, processingDate, fileNumber);
            var fileName = runReference + ".txt";

            var run = new PaymentRun
            {
                CreatedAt = _clock.Now,
                CreatedBy = "housing-jobs",
                ProcessingDate = processingDate,
                FileName = fileName,
                PaymentCount = payments.Count,
                Total = payments.Sum(p => p.Amount),
                Status = "Created"
            };
            _db.PaymentRuns.Add(run);
            await _db.SaveChangesAsync();

            await _bureau.UploadAsync(fileName, content);
            Directory.CreateDirectory(_options.BacsArchiveFolder);
            await File.WriteAllTextAsync(Path.Combine(_options.BacsArchiveFolder, fileName), content, Encoding.ASCII);

            run.SubmittedAt = _clock.Now;
            run.Status = "Submitted";
            foreach (var invoice in payable.SelectMany(g => g))
            {
                invoice.Status = InvoiceStatus.InPaymentRun;
                invoice.PaymentRunId = run.Id;
                invoice.ModifiedAt = _clock.Now;
                invoice.ModifiedBy = "housing-jobs";
            }
            await _db.SaveChangesAsync();

            foreach (var group in payable)
                await SendRemittance(group.Key, group.ToList(), runReference, processingDate);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Contractor payment run {Run}: {Count} payments, total {Total:N2}, processing {Date:dd/MM/yyyy}; skipped (no bank details): {Skipped}",
                runReference, payments.Count, run.Total, processingDate, string.Join(", ", skipped));
        }

        private async Task SendRemittance(Contractor contractor, IList<ContractorInvoice> invoices, string runReference, DateTime paymentDate)
        {
            var uk = CultureInfo.GetCultureInfo("en-GB");
            var table = new StringBuilder("<table class=\"lines\"><tr><th>Invoice</th><th>Date</th><th>Job</th><th>Net</th><th>VAT</th><th>Total</th></tr>");
            foreach (var i in invoices.OrderBy(i => i.InvoiceDate))
                table.Append($"<tr><td>{WebUtility.HtmlEncode(i.InvoiceNumber)}</td><td>{i.InvoiceDate:dd/MM/yyyy}</td><td>{i.WorkOrder?.Reference}</td><td>{i.NetAmount.ToString("N2", uk)}</td><td>{i.VatAmount.ToString("N2", uk)}</td><td>{i.GrossAmount.ToString("N2", uk)}</td></tr>");
            table.Append("</table>");

            var html = _letters.Compose(new LetterModel
            {
                TemplateCode = "ContractorRemittance",
                RecipientName = contractor.Name,
                AddressLines = new List<string> { "Accounts department" },
                Date = _clock.Today,
                Reference = runReference,
                Fields = new Dictionary<string, string>
                {
                    ["Salutation"] = "Sir or Madam",
                    ["RunReference"] = runReference,
                    ["PaymentDate"] = paymentDate.ToString("d MMMM yyyy"),
                    ["Total"] = invoices.Sum(i => i.GrossAmount).ToString("N2", uk),
                    ["AccountEnding"] = contractor.BankAccountNumber.Substring(contractor.BankAccountNumber.Length - 4),
                    ["InvoiceTableHtml"] = table.ToString()
                }
            });
            var pdf = _pdf.RenderHtml(html, "Remittance " + runReference);
            var to = contractor.RemittanceEmail ?? contractor.ContactEmail;
            await _messenger.EmailAsync(null, contractor.Id, to, "ContractorRemittance", "Fabrikam Homes remittance advice " + runReference,
                $"<p>Please find attached our remittance advice for payment of {invoices.Count} invoice(s), to reach your account on {paymentDate:dd/MM/yyyy}.</p><p>Fabrikam Homes Finance</p>",
                new[] { new EmailAttachment { FileName = "Remittance_" + runReference + "_" + contractor.Code + ".pdf", ContentType = "application/pdf", Content = pdf } });
        }

        private static bool HasBankDetails(Contractor c) =>
            !string.IsNullOrWhiteSpace(c.BankSortCode) && !string.IsNullOrWhiteSpace(c.BankAccountNumber) && c.BankAccountNumber.Length >= 8;
    }
}
