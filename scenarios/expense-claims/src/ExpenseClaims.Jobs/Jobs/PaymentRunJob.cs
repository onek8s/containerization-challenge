using ExpenseClaims.Data.Entities;
using ExpenseClaims.Jobs.Bank;
using Microsoft.Extensions.Options;
using Quartz;

namespace ExpenseClaims.Jobs.Jobs;

/// <summary>Creates the day's payment file from checked claims and uploads it to the bank gateway.</summary>
[DisallowConcurrentExecution]
public sealed class PaymentRunJob : IJob
{
    private readonly PaymentRunService _runs;
    private readonly BankGatewayClient _gateway;
    private readonly BankOptions _options;
    private readonly Data.ExpenseClaimsDbContext _db;
    private readonly ILogger<PaymentRunJob> _logger;

    public PaymentRunJob(PaymentRunService runs, BankGatewayClient gateway, IOptions<BankOptions> options, Data.ExpenseClaimsDbContext db, ILogger<PaymentRunJob> logger)
    {
        _runs = runs;
        _gateway = gateway;
        _options = options.Value;
        _db = db;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var created = await _runs.CreateAsync(DateTime.Now, ct);
        if (created is null)
        {
            _logger.LogInformation("No checked claims to pay");
            return;
        }

        var run = created.Run;
        if (!_options.UsesGateway)
        {
            // The bank's file transfer client picks the file up from the outbound folder.
            run.Status = PaymentRunStatus.Sent;
            run.SentAt = DateTime.Now;
            await _runs.CompleteAsync(run, true, null, DateTime.Now, ct);
            return;
        }

        try
        {
            var result = await _gateway.UploadPain001Async(run.FileName, created.Content, ct);
            run.Status = PaymentRunStatus.Sent;
            run.SentAt = DateTime.Now;
            run.BankReference = result.FileReference;
            _logger.LogInformation("Payment file {MessageId} sent, bank reference {Reference}", run.MessageId, result.FileReference);
        }
        catch (Exception ex)
        {
            run.Error = ex.Message;
            _logger.LogError(ex, "Payment file {MessageId} could not be sent; it is retried by the status job", run.MessageId);
        }
        await _db.SaveChangesAsync(ct);
    }
}
