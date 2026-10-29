using System.Globalization;
using ExpenseClaims.Core.Claims;
using ExpenseClaims.Core.Payments;
using ExpenseClaims.Data;
using ExpenseClaims.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExpenseClaims.Jobs.Bank;

/// <summary>Collects checked claims into a SEPA payment file, one payment per employee.</summary>
public sealed class PaymentRunService
{
    private readonly ExpenseClaimsDbContext _db;
    private readonly BankOptions _options;
    private readonly ILogger<PaymentRunService> _logger;

    public PaymentRunService(ExpenseClaimsDbContext db, IOptions<BankOptions> options, ILogger<PaymentRunService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public sealed record CreatedRun(PaymentRun Run, byte[] Content, string Path);

    public async Task<CreatedRun?> CreateAsync(DateTime now, CancellationToken ct)
    {
        var claims = await _db.Claims
            .Include(c => c.Employee)
            .Where(c => c.Status == ClaimStatus.Checked && c.PaymentRunId == null)
            .OrderBy(c => c.Number)
            .ToListAsync(ct);
        // Claims with only company card lines have nothing to pay to the employee.
        foreach (var cardOnly in claims.Where(c => c.Reimbursable == 0))
        {
            cardOnly.Status = ClaimStatus.Paid;
            cardOnly.PaidAt = now;
            cardOnly.Events.Add(new ClaimEvent { At = now, Actor = "payment-run", Action = "Closed", Details = "Nothing to reimburse" });
        }
        claims = claims.Where(c => c.Reimbursable > 0).ToList();

        var payable = claims.Where(c => Iban.IsValid(c.Employee.Iban)).ToList();
        foreach (var skipped in claims.Except(payable))
            _logger.LogWarning("Claim {ClaimNumber} skipped: {Employee} has no valid bank account", skipped.Number, skipped.Employee.EmployeeNumber);
        if (payable.Count == 0)
        {
            await _db.SaveChangesAsync(ct);
            return null;
        }

        var executionDate = ExecutionDate(now, _options.CutOffTime);
        var messageId = string.Create(CultureInfo.InvariantCulture, $"LAMNA-EXP-{now:yyyyMMddHHmmss}");
        var payments = payable
            .GroupBy(c => c.Employee)
            .Select(g => new PaymentInstruction(
                EndToEndId: $"{messageId[10..]}-{g.Key.EmployeeNumber}",
                CreditorName: g.Key.DisplayName,
                CreditorIban: g.Key.Iban!,
                CreditorBic: g.Key.Bic,
                Amount: g.Sum(c => c.Reimbursable),
                RemittanceInfo: "Expense claims " + string.Join(" ", g.Select(c => c.Number))))
            .ToList();

        var content = Pain001Writer.Write(messageId, now, executionDate, new DebtorAccount(
            _options.DebtorName, _options.DebtorIban, _options.DebtorBic, _options.DebtorOrganisationId), payments);

        Directory.CreateDirectory(_options.OutboundFolder);
        var fileName = messageId + ".xml";
        var path = Path.Combine(_options.OutboundFolder, fileName);
        var temp = path + ".tmp";
        await File.WriteAllBytesAsync(temp, content, ct);

        var run = new PaymentRun
        {
            MessageId = messageId,
            CreatedAt = now,
            ExecutionDate = executionDate,
            PaymentCount = payments.Count,
            Total = payments.Sum(p => p.Amount),
            FileName = fileName,
            Status = PaymentRunStatus.Created
        };
        _db.PaymentRuns.Add(run);
        foreach (var claim in payable)
        {
            claim.PaymentRun = run;
            claim.Status = ClaimStatus.InPayment;
            claim.Events.Add(new ClaimEvent { At = now, Actor = "payment-run", Action = "InPayment", Details = messageId });
        }
        await _db.SaveChangesAsync(ct);
        File.Move(temp, path, overwrite: true);

        _logger.LogInformation("Payment run {MessageId}: {Count} payments, {Total} EUR, execution {ExecutionDate}",
            messageId, run.PaymentCount, run.Total, executionDate);
        return new CreatedRun(run, content, path);
    }

    /// <summary>Marks the claims of an accepted run paid, or returns them to Checked when the bank rejected the file.</summary>
    public async Task CompleteAsync(PaymentRun run, bool accepted, string? reason, DateTime now, CancellationToken ct)
    {
        var claims = await _db.Claims.Where(c => c.PaymentRunId == run.Id).ToListAsync(ct);
        foreach (var claim in claims)
        {
            if (accepted)
            {
                claim.Status = ClaimStatus.Paid;
                claim.PaidAt = now;
                claim.Events.Add(new ClaimEvent { At = now, Actor = "payment-run", Action = "Paid", Details = run.BankReference });
            }
            else
            {
                claim.Status = ClaimStatus.Checked;
                claim.PaymentRunId = null;
                claim.Events.Add(new ClaimEvent { At = now, Actor = "payment-run", Action = "PaymentFailed", Details = reason });
            }
        }
        run.Status = accepted ? PaymentRunStatus.Accepted : PaymentRunStatus.Failed;
        run.Error = accepted ? null : reason;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Same day before the cut-off time, otherwise the next business day.</summary>
    public static DateOnly ExecutionDate(DateTime now, TimeSpan cutOff)
    {
        var date = DateOnly.FromDateTime(now);
        if (now.TimeOfDay >= cutOff || IsWeekend(date))
        {
            do
                date = date.AddDays(1);
            while (IsWeekend(date));
        }
        return date;
    }

    private static bool IsWeekend(DateOnly d) => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}
