using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductReturns.Core;
using ProductReturns.Data;
using ProductReturns.Jobs.Infrastructure;

namespace ProductReturns.Jobs.Jobs
{
    /// <summary>
    /// Writes all returns inspected before today (head office time) to a refund batch file in the
    /// finance inbound folder and marks them exported.
    /// </summary>
    public class RefundExportJob : IJob
    {
        private readonly ReturnsRepository _repository;
        private readonly BusinessCalendar _calendar;
        private readonly JobOptions _options;
        private readonly JobMailer _mailer;
        private readonly ILogger<RefundExportJob> _logger;

        public RefundExportJob(ReturnsRepository repository, BusinessCalendar calendar, IOptions<JobOptions> options,
            JobMailer mailer, ILogger<RefundExportJob> logger)
        {
            _repository = repository;
            _calendar = calendar;
            _options = options.Value;
            _mailer = mailer;
            _logger = logger;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            var cutoffUtc = _calendar.TodayStartUtc;
            var due = await _repository.RefundsDueAsync(cutoffUtc, cancellationToken);
            if (due.Count == 0)
            {
                _logger.LogInformation("No refunds inspected before {Cutoff:u}", cutoffUtc);
                return;
            }

            var batchId = RefundBatchFile.BatchId(_calendar.LocalNow);
            var lastFile = Path.Combine(_options.StateFolder, "refunds.last");
            Directory.CreateDirectory(_options.StateFolder);
            if (File.Exists(lastFile) && File.ReadAllText(lastFile).Trim() == batchId)
            {
                _logger.LogWarning("Batch {Batch} was already written, run again in a minute", batchId);
                return;
            }

            var fileName = RefundBatchFile.FileName(batchId);
            Directory.CreateDirectory(_options.RefundArchive);
            var archivePath = Path.Combine(_options.RefundArchive, fileName);
            using (var writer = new StreamWriter(archivePath, false, RefundBatchFile.FileEncoding))
            {
                RefundBatchFile.Write(writer, batchId, due);
            }

            // Finance polls the inbound folder; write under a temporary name and rename when complete.
            Directory.CreateDirectory(_options.RefundOutbox);
            var target = Path.Combine(_options.RefundOutbox, fileName);
            var temp = target + ".tmp";
            File.Copy(archivePath, temp, true);
            File.Move(temp, target);

            var now = DateTime.UtcNow;
            foreach (var request in due)
            {
                request.Status = ReturnStatus.RefundExported;
                request.RefundExportedUtc = now;
                request.RefundBatch = batchId;
                request.Notes.Add(new ReturnNote { Author = "jobs", Text = "Refund sent to finance in batch " + batchId + ".", CreatedUtc = now });
            }

            await _repository.SaveAsync(cancellationToken);
            File.WriteAllText(lastFile, batchId);

            var total = due.Sum(r => r.RefundAmount ?? 0m);
            _logger.LogInformation("Refund batch {Batch}: {Count} returns, {Total:0.00} EUR, written to {Target}", batchId, due.Count, total, target);

            if (!string.IsNullOrEmpty(_mailer.Options.FinanceTo))
            {
                await _mailer.SendAsync(_mailer.Options.FinanceTo, "Accounts payable", "Refund batch " + batchId,
                    $"<p>Refund batch {batchId} with {due.Count} refunds, total {total:0.00} EUR, is in the inbound folder.</p>", archivePath);
            }
        }
    }
}
