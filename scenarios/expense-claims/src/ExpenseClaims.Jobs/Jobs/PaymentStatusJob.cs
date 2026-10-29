using ExpenseClaims.Data;
using ExpenseClaims.Data.Entities;
using ExpenseClaims.Jobs.Bank;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quartz;

namespace ExpenseClaims.Jobs.Jobs;

/// <summary>
/// Follows payment files at the bank gateway: re-sends files that were not delivered and
/// completes the claims when the bank reports the file accepted (ACCP/ACSC) or rejected (RJCT).
/// </summary>
[DisallowConcurrentExecution]
public sealed class PaymentStatusJob : IJob
{
    private readonly ExpenseClaimsDbContext _db;
    private readonly PaymentRunService _runs;
    private readonly BankGatewayClient _gateway;
    private readonly BankOptions _options;
    private readonly ILogger<PaymentStatusJob> _logger;

    public PaymentStatusJob(ExpenseClaimsDbContext db, PaymentRunService runs, BankGatewayClient gateway, IOptions<BankOptions> options, ILogger<PaymentStatusJob> logger)
    {
        _db = db;
        _runs = runs;
        _gateway = gateway;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        if (!_options.UsesGateway)
            return;

        var ct = context.CancellationToken;
        var open = await _db.PaymentRuns.Where(r => r.Status == PaymentRunStatus.Created || r.Status == PaymentRunStatus.Sent).ToListAsync(ct);
        foreach (var run in open)
        {
            try
            {
                if (run.Status == PaymentRunStatus.Created)
                {
                    var path = Path.Combine(_options.OutboundFolder, run.FileName);
                    var result = await _gateway.UploadPain001Async(run.FileName, await File.ReadAllBytesAsync(path, ct), ct);
                    run.Status = PaymentRunStatus.Sent;
                    run.SentAt = DateTime.Now;
                    run.BankReference = result.FileReference;
                    run.Error = null;
                    await _db.SaveChangesAsync(ct);
                    continue;
                }

                var status = await _gateway.GetStatusAsync(run.BankReference!, ct);
                switch (status.Status)
                {
                    case "ACCP" or "ACSC" or "ACSP":
                        await _runs.CompleteAsync(run, true, null, DateTime.Now, ct);
                        _logger.LogInformation("Payment file {MessageId} accepted by the bank ({Status})", run.MessageId, status.Status);
                        break;
                    case "RJCT":
                        await _runs.CompleteAsync(run, false, status.Reason ?? "Rejected by the bank", DateTime.Now, ct);
                        _logger.LogError("Payment file {MessageId} rejected by the bank: {Reason}", run.MessageId, status.Reason);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Status of payment file {MessageId} could not be checked", run.MessageId);
            }
        }
    }
}
