using ExpenseClaims.Core.Accounting;
using ExpenseClaims.Core.Claims;
using ExpenseClaims.Data;
using ExpenseClaims.Data.Entities;
using ExpenseClaims.Jobs.Erp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quartz;

namespace ExpenseClaims.Jobs.Jobs;

/// <summary>
/// Sends checked claims to the ERP general journal (batch EXPCLAIMS). Finance reviews and posts the batch in NAV.
/// A claim whose document number is already in the batch is only marked exported, so a run can be repeated.
/// </summary>
[DisallowConcurrentExecution]
public sealed class AccountingExportJob : IJob
{
    private static readonly ClaimStatus[] Exportable = { ClaimStatus.Checked, ClaimStatus.InPayment, ClaimStatus.Paid };

    private readonly ExpenseClaimsDbContext _db;
    private readonly NavJournalClient _nav;
    private readonly JournalBuilder _journal;
    private readonly ErpOptions _options;
    private readonly ILogger<AccountingExportJob> _logger;

    public AccountingExportJob(ExpenseClaimsDbContext db, NavJournalClient nav, JournalBuilder journal, IOptions<ErpOptions> options, ILogger<AccountingExportJob> logger)
    {
        _db = db;
        _nav = nav;
        _journal = journal;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var claims = await _db.Claims
            .Include(c => c.Employee)
            .Include(c => c.Lines)
            .Where(c => Exportable.Contains(c.Status) && c.ExportedAt == null)
            .OrderBy(c => c.CheckedAt)
            .Take(_options.MaxClaimsPerRun)
            .AsSplitQuery()
            .ToListAsync(ct);
        if (claims.Count == 0)
            return;

        var lineNo = await _nav.GetLastLineNoAsync(ct);
        var exported = 0;
        foreach (var claim in claims)
        {
            var lines = _journal.Build(ToJournal(claim));
            try
            {
                if (!await _nav.DocumentExistsAsync(claim.Number, ct))
                {
                    foreach (var line in lines)
                        await _nav.InsertAsync(line, lineNo += 10000, ct);
                }
                claim.JournalDocumentNo = claim.Number;
                claim.ExportedAt = DateTime.Now;
                claim.Events.Add(new ClaimEvent { At = DateTime.Now, Actor = "accounting-export", Action = "Exported", Details = $"{_options.JournalBatch}, {lines.Count} lines" });
                await _db.SaveChangesAsync(ct);
                exported++;
            }
            catch (HttpRequestException ex)
            {
                // A half-written document stays in the batch; the next run sees it and does not duplicate it.
                _logger.LogError(ex, "Claim {ClaimNumber} could not be exported to the ERP", claim.Number);
            }
        }
        _logger.LogInformation("Accounting export: {Exported} of {Count} claims sent to journal batch {Batch}", exported, claims.Count, _options.JournalBatch);
    }

    private static JournalClaim ToJournal(ExpenseClaim c) => new(
        c.Number,
        c.Employee.EmployeeNumber,
        c.Employee.DisplayName,
        c.CostCenter,
        c.ProjectCode,
        DateOnly.FromDateTime(c.CheckedAt ?? DateTime.Today),
        c.Lines.Select(l => new JournalClaimLine(l.LineNumber, l.Category, l.Description, l.AmountEur, l.VatAmountEur, l.PaidByCompany)).ToList());
}
