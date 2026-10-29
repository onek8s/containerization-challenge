using System.Text.Json;
using Lucerne.Lending.CoreBanking;
using Lucerne.Lending.Data;
using Lucerne.Lending.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lucerne.Lending.Workflow;

public sealed record StageRunResult(int Cases, int Escalated, int FeesCharged, int Letters, int HandedOver, int PromisesBroken);

/// <summary>Collections: keeps the loan copy in step with core events and escalates overdue loans (manual CM-07).</summary>
public sealed class CollectionsService
{
    private readonly LoanRepository _loans;
    private readonly CollectionRepository _cases;
    private readonly LedgerRepository _ledger;
    private readonly OutboxRepository _outbox;
    private readonly Db _db;
    private readonly ICoreLoanService _coreLoans;
    private readonly ICoreCustomerService _customers;
    private readonly ICoreReferenceData _referenceData;
    private readonly DelinquencyPolicy _policy;
    private readonly LendingOptions _options;
    private readonly ILogger<CollectionsService> _log;

    public CollectionsService(LoanRepository loans, CollectionRepository cases, LedgerRepository ledger, OutboxRepository outbox, Db db,
        ICoreLoanService coreLoans, ICoreCustomerService customers, ICoreReferenceData referenceData, DelinquencyPolicy policy, IOptions<LendingOptions> options,
        ILogger<CollectionsService> log)
    {
        _loans = loans;
        _cases = cases;
        _ledger = ledger;
        _outbox = outbox;
        _db = db;
        _coreLoans = coreLoans;
        _customers = customers;
        _referenceData = referenceData;
        _policy = policy;
        _options = options.Value;
        _log = log;
    }

    public async Task HandleEventAsync(CoreLoanEvent evt, CancellationToken ct)
    {
        var loan = await _loans.GetAsync(evt.AccountNumber, ct);
        if (loan == null)
        {
            // loans opened before the lending platform: take them over from the core
            var core = await _coreLoans.GetLoanAsync(evt.AccountNumber, ct);
            if (core == null || string.IsNullOrEmpty(core.CustomerNumber))
            {
                _log.LogWarning("Event {Type} for unknown loan {Account} ignored", evt.EventType, evt.AccountNumber);
                return;
            }
            loan = new LoanRow
            {
                AccountNumber = core.AccountNumber,
                CustomerNumber = core.CustomerNumber,
                ProductCode = core.ProductCode,
                Amount = core.OriginalAmount,
                NominalRatePercent = core.NominalRatePercent,
                MonthlyInstalment = core.NextInstalment,
                BookedUtc = DateTime.UtcNow,
                DisbursedUtc = DateTime.UtcNow,
                Balance = core.Balance,
                Arrears = core.Arrears,
                DaysPastDue = core.DaysPastDue,
                CoreStatus = core.Status
            };
            await _loans.AddAsync(loan);
        }

        decimal? balance = null;
        string? status = null;
        if (evt.EventType is CoreLoanEvent.Paid or CoreLoanEvent.Settled)
        {
            var core = await _coreLoans.GetLoanAsync(evt.AccountNumber, ct);
            balance = core?.Balance;
            status = core?.Status;
        }
        await _loans.UpdateFromCoreAsync(evt.AccountNumber, balance, evt.Arrears, evt.DaysPastDue, status, evt.Timestamp);

        var open = await _cases.OpenCaseAsync(evt.AccountNumber, ct);
        switch (evt.EventType)
        {
            case CoreLoanEvent.Missed or CoreLoanEvent.DaysPastDueChanged when evt.Arrears >= _policy.MinimumArrears:
                if (open == null)
                {
                    var created = await _cases.CreateAsync(evt.AccountNumber, loan.CustomerNumber, evt.Arrears, evt.DaysPastDue, ct);
                    _log.LogInformation("Collection case {Case} opened for {Account}", created.Reference, evt.AccountNumber);
                }
                else
                {
                    await _cases.UpdateArrearsAsync(open.Id, evt.Arrears, evt.DaysPastDue);
                }
                break;
            case CoreLoanEvent.Paid or CoreLoanEvent.Settled when open != null:
                await _cases.UpdateArrearsAsync(open.Id, evt.Arrears, evt.DaysPastDue);
                await _cases.AddActionAsync(open.Id, "Payment", $"{evt.Amount:0.00} EUR received, arrears {evt.Arrears:0.00}", "CORE");
                if (evt.Arrears <= 0 && open.StatusValue != CollectionStatus.HandedOver)
                {
                    await _cases.SetStatusAsync(open.Id, CollectionStatus.Closed);
                    await _cases.AddActionAsync(open.Id, "Closed", "Arrears paid", "SYSTEM");
                }
                break;
        }
    }

    public async Task<StageRunResult> RunDailyStagesAsync(CancellationToken ct)
    {
        int escalated = 0, fees = 0, letters = 0, handed = 0, broken = 0;
        var today = await _referenceData.GetBusinessDateAsync(ct);
        var cases = await _cases.OpenCasesAsync(ct);
        foreach (var c in cases)
        {
            var current = c;
            if (current.StatusValue == CollectionStatus.Promised)
            {
                if (current.PromiseDate is { } promised && promised < today && current.Arrears > 0)
                {
                    await _cases.SetStatusAsync(current.Id, CollectionStatus.Open);
                    await _cases.AddActionAsync(current.Id, "PromiseBroken", $"Promise {promised:d.M.yyyy} {current.PromiseAmount:0.00} EUR not kept", "SYSTEM");
                    broken++;
                    current = (await _cases.GetAsync(current.Id, ct))!;
                }
                else continue;
            }
            if (current.StatusValue == CollectionStatus.PaymentPlan) continue;

            var stage = _policy.StageFor(current.DaysPastDue, current.Arrears);
            if (stage <= current.StageValue) continue;

            var chargeFee = _policy.ChargesFee(stage, current.FeesCharged);
            if (chargeFee)
            {
                var fee = await _coreLoans.ChargeFeeAsync(current.AccountNumber, "REM", _policy.ReminderFee, $"Reminder fee {stage}", ct);
                if (!fee.Success)
                {
                    _log.LogWarning("Reminder fee for {Account} rejected: {Message}", current.AccountNumber, fee.Message);
                    chargeFee = false;
                }
            }

            if (stage == CollectionStage.Agency)
            {
                var term = await _coreLoans.TerminateAsync(current.AccountNumber, today, _options.SystemUser, ct);
                if (!term.Success)
                {
                    _log.LogError("Termination of {Account} rejected: {Message}", current.AccountNumber, term.Message);
                    continue;
                }
            }

            var recipient = await RecipientAsync(current.CustomerNumber, ct);
            var values = new Dictionary<string, string>
            {
                ["account"] = current.AccountNumber,
                ["arrears"] = current.Arrears.ToString("0.00"),
                ["dpd"] = current.DaysPastDue.ToString(),
                ["fee"] = chargeFee ? _policy.ReminderFee.ToString("0.00") : "0.00",
                ["reference"] = PaymentReference.ForLoanAccount(current.AccountNumber),
                ["dueDate"] = today.AddDays(14).ToString("d.M.yyyy")
            };
            await _db.InTransactionAsync(async tx =>
            {
                if (!await _cases.MoveStageAsync(current.Id, stage, chargeFee, current.Version, tx)) return false;
                await _cases.AddActionAsync(current.Id, "Stage." + stage, $"{current.DaysPastDue} days past due, arrears {current.Arrears:0.00}" +
                    (chargeFee ? $", fee {_policy.ReminderFee:0.00}" : ""), "SYSTEM", tx);
                if (chargeFee)
                    await _ledger.AddAsync(new[]
                    {
                        new GlEntryRow { BusinessDate = today, GlAccount = GlAccounts.LoansReceivable, CostCenter = _options.CostCenter, Debit = _policy.ReminderFee,
                            Text = $"Reminder fee {current.AccountNumber}", SourceType = "REMFEE", SourceRef = $"{current.Reference}/{stage}" },
                        new GlEntryRow { BusinessDate = today, GlAccount = GlAccounts.ReminderFeeIncome, CostCenter = _options.CostCenter, Credit = _policy.ReminderFee,
                            Text = $"Reminder fee {current.AccountNumber}", SourceType = "REMFEE", SourceRef = $"{current.Reference}/{stage}" }
                    }, tx);
                var template = _policy.LetterTemplate(stage);
                if (template.Length > 0)
                    await _outbox.EnqueueAsync(Topics.DocumentRequested, new DocumentRequest(DocumentKinds.CollectionLetter, null, current.Id, template, recipient, values), tx);
                return true;
            }, ct);
            escalated++;
            if (chargeFee) fees++;
            if (_policy.LetterTemplate(stage).Length > 0) letters++;
            if (stage == CollectionStage.Agency) handed++;
        }
        return new StageRunResult(cases.Count, escalated, fees, letters, handed, broken);
    }

    public async Task<PaymentPlanRow> CreatePaymentPlanAsync(long caseId, int months, string officer, CancellationToken ct)
    {
        var c = await _cases.GetAsync(caseId, ct) ?? throw new KeyNotFoundException(caseId.ToString());
        if (c.StatusValue is CollectionStatus.HandedOver or CollectionStatus.Closed)
            throw new ConflictException("Payment plans cannot be made for closed or handed over cases.");
        var loan = await _coreLoans.GetLoanAsync(c.AccountNumber, ct) ?? throw new KeyNotFoundException(c.AccountNumber);
        var today = await _referenceData.GetBusinessDateAsync(ct);
        var first = loan.NextDueDate ?? today.AddMonths(1);
        var plan = PaymentPlanCalculator.Spread(loan.Arrears, months, first, loan.NextInstalment);
        var result = await _coreLoans.SetPaymentPlanAsync(c.AccountNumber, plan.Select(p => new PlanInstalmentRequest(p.DueDate, p.Amount)).ToList(),
            officer.Length > 10 ? officer[..10].ToUpperInvariant() : officer.ToUpperInvariant(), ct);
        if (!result.Success) throw new ConflictException($"Core rejected the plan: {result.Message} ({result.ReturnCode})");
        var recipient = await RecipientAsync(c.CustomerNumber, ct);
        var row = new PaymentPlanRow
        {
            CaseId = caseId,
            AccountNumber = c.AccountNumber,
            Months = months,
            InstalmentsJson = JsonSerializer.Serialize(plan),
            Status = "Active",
            CreatedBy = officer,
            CreatedUtc = DateTime.UtcNow
        };
        await _db.InTransactionAsync(async tx =>
        {
            await _cases.AddPlanAsync(row, tx);
            await _cases.SetStatusAsync(caseId, CollectionStatus.PaymentPlan, tx);
            await _cases.AddActionAsync(caseId, "PaymentPlan", $"{months} months, first {plan[0].Amount:0.00} EUR on {plan[0].DueDate:d.M.yyyy}", officer, tx);
            await _outbox.EnqueueAsync(Topics.DocumentRequested, new DocumentRequest(DocumentKinds.PaymentPlanConfirmation, null, caseId, null,
                recipient, new Dictionary<string, string> { ["account"] = c.AccountNumber, ["reference"] = PaymentReference.ForLoanAccount(c.AccountNumber) }), tx);
            return true;
        }, ct);
        return row;
    }

    private async Task<Recipient?> RecipientAsync(string customerNumber, CancellationToken ct)
    {
        var customer = await _customers.GetAsync(customerNumber, ct);
        return customer == null ? null : new Recipient(customer.Name, customer.Street, customer.PostalCode, customer.City, null);
    }

    public async Task RecordPromiseAsync(long caseId, DateOnly date, decimal amount, string officer, CancellationToken ct)
    {
        var today = await _referenceData.GetBusinessDateAsync(ct);
        if (date < today || date > today.AddDays(30)) throw new ValidationFailedException(new[] { "Promise date must be within 30 days." });
        await _cases.SetPromiseAsync(caseId, date, amount);
        await _cases.AddActionAsync(caseId, "Promise", $"{amount:0.00} EUR by {date:d.M.yyyy}", officer);
    }

    /// <summary>Plans with a missed plan instalment are cancelled and the case continues in collections.</summary>
    public async Task<int> CheckPlansAsync(CancellationToken ct)
    {
        var count = 0;
        var today = await _referenceData.GetBusinessDateAsync(ct);
        foreach (var plan in await _cases.ActivePlansAsync(ct))
        {
            var instalments = JsonSerializer.Deserialize<List<PlanInstalment>>(plan.InstalmentsJson)!;
            var loan = await _loans.GetAsync(plan.AccountNumber, ct);
            if (loan == null) continue;
            if (instalments.Last().DueDate < today && loan.Arrears <= 0)
            {
                await _cases.SetPlanStatusAsync(plan.Id, "Completed");
                await _cases.SetStatusAsync(plan.CaseId, CollectionStatus.Closed);
                await _cases.AddActionAsync(plan.CaseId, "PlanCompleted", null, "SYSTEM");
                count++;
            }
            else if (loan.DaysPastDue > 14 && instalments.Any(i => i.DueDate < today.AddDays(-14)))
            {
                await _cases.SetPlanStatusAsync(plan.Id, "Broken");
                await _cases.SetStatusAsync(plan.CaseId, CollectionStatus.Open);
                await _cases.AddActionAsync(plan.CaseId, "PlanBroken", $"{loan.DaysPastDue} days past due", "SYSTEM");
                count++;
            }
        }
        return count;
    }
}
