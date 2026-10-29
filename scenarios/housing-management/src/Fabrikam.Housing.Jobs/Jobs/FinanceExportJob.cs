using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Contractors;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Finance;
using Fabrikam.Housing.Rent;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fabrikam.Housing.Jobs.Jobs
{
    /// <summary>
    /// Nightly journal to Sage 200: rent account movements summarised by day, type and patch, and
    /// approved contractor invoices. Sage picks the file up from the outbound folder at 05:00.
    /// </summary>
    [AutomaticRetry(Attempts = 2)]
    [DisableConcurrentExecution(1800)]
    public class FinanceExportJob : IHousingJob
    {
        private readonly HousingDbContext _db;
        private readonly IClock _clock;
        private readonly JobsOptions _options;
        private readonly ILogger<FinanceExportJob> _logger;

        public FinanceExportJob(HousingDbContext db, IClock clock, IOptions<JobsOptions> options, ILogger<FinanceExportJob> logger)
        {
            _db = db;
            _clock = clock;
            _options = options.Value;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            var cutOff = _clock.Now;
            var rent = await (from t in _db.RentTransactions
                              join tn in _db.Tenancies on t.TenancyId equals tn.Id
                              join p in _db.Properties on tn.PropertyId equals p.Id
                              where t.JournalBatchId == null && t.PostedOn <= cutOff
                              select new { Tx = t, p.PatchCode }).ToListAsync();
            var invoiceStatuses = new[] { InvoiceStatus.Approved, InvoiceStatus.InPaymentRun, InvoiceStatus.Paid };
            var invoices = await _db.ContractorInvoices.Include(i => i.Contractor).Include(i => i.WorkOrder).ThenInclude(w => w.Property)
                .Where(i => i.JournalBatchId == null && invoiceStatuses.Contains(i.Status))
                .ToListAsync();
            if (rent.Count == 0 && invoices.Count == 0)
            {
                _logger.LogInformation("Finance export: nothing to export");
                return;
            }

            var builder = new JournalBuilder();
            var lines = new List<JournalLine>();
            lines.AddRange(builder.FromRentTransactions(rent.Select(r => (r.Tx, r.PatchCode ?? ""))));
            lines.AddRange(builder.FromInvoices(invoices, i => i.WorkOrder?.Property?.PatchCode ?? ""));

            var debit = lines.Sum(l => l.Debit);
            var credit = lines.Sum(l => l.Credit);
            if (debit != credit)
                throw new InvalidOperationException($"Journal does not balance: debits {debit:N2}, credits {credit:N2}.");

            Directory.CreateDirectory(_options.FinanceOutbound);
            var fileName = "FAB_RENT_" + _clock.Today.ToString("yyyyMMdd") + ".csv";
            var path = Path.Combine(_options.FinanceOutbound, fileName);
            for (var n = 2; File.Exists(path); n++)
            {
                fileName = "FAB_RENT_" + _clock.Today.ToString("yyyyMMdd") + "_" + n + ".csv";
                path = Path.Combine(_options.FinanceOutbound, fileName);
            }

            var batch = new JournalBatch
            {
                CreatedAt = _clock.Now,
                PeriodEnd = cutOff,
                FileName = fileName,
                LineCount = lines.Count,
                TotalDebit = Money.Round(debit),
                Status = "Writing"
            };
            _db.JournalBatches.Add(batch);
            await _db.SaveChangesAsync();

            // Write to a temporary name first so Sage never picks up half a file.
            var temp = path + ".tmp";
            await File.WriteAllTextAsync(temp, JournalBuilder.ToCsv(lines), Encoding.GetEncoding(1252));
            File.Move(temp, path);

            foreach (var r in rent) r.Tx.JournalBatchId = batch.Id;
            foreach (var i in invoices) i.JournalBatchId = batch.Id;
            batch.Status = "Written";
            await _db.SaveChangesAsync();
            _logger.LogInformation("Finance export {File}: {Lines} journal lines from {Rent} rent transactions and {Invoices} invoices, debits {Debit:N2}",
                fileName, lines.Count, rent.Count, invoices.Count, debit);
        }
    }
}
