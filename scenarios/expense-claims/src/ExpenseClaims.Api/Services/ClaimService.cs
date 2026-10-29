using ExpenseClaims.Api.Authentication;
using ExpenseClaims.Api.Models;
using ExpenseClaims.Core.Accounting;
using ExpenseClaims.Core.Claims;
using ExpenseClaims.Core.Currency;
using ExpenseClaims.Core.Payments;
using ExpenseClaims.Core.Policy;
using ExpenseClaims.Data;
using ExpenseClaims.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExpenseClaims.Api.Services;

public sealed class ClaimService
{
    private readonly ExpenseClaimsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly ExpensePolicy _policy;
    private readonly ClaimNumberAllocator _numbers;
    private readonly IReceiptStore _receipts;
    private readonly ApprovalOptions _approval;
    private readonly VatRules _vat;
    private readonly ILogger<ClaimService> _logger;

    public ClaimService(
        ExpenseClaimsDbContext db,
        ICurrentUser user,
        ExpensePolicy policy,
        ClaimNumberAllocator numbers,
        IReceiptStore receipts,
        IOptions<ApprovalOptions> approval,
        VatRules vat,
        ILogger<ClaimService> logger)
    {
        _db = db;
        _user = user;
        _policy = policy;
        _numbers = numbers;
        _receipts = receipts;
        _approval = approval.Value;
        _vat = vat;
        _logger = logger;
    }

    private static DateTime Now => DateTime.Now;
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public async Task<IReadOnlyList<ClaimSummaryDto>> ListMineAsync(CancellationToken ct)
    {
        var me = await RequireEmployeeAsync(ct);
        var claims = await _db.Claims.AsNoTracking()
            .Where(c => c.EmployeeId == me.Id)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new { c, LineCount = c.Lines.Count })
            .ToListAsync(ct);
        return claims.Select(x => Summary(x.c, me.DisplayName, x.LineCount)).ToList();
    }

    public async Task<ClaimDto> GetAsync(int id, CancellationToken ct)
    {
        var claim = await LoadAsync(id, ct);
        await RequireCanViewAsync(claim, ct);
        return await ToDtoAsync(claim, ct);
    }

    public async Task<ClaimDto> CreateAsync(CreateClaimRequest request, CancellationToken ct)
    {
        var me = await RequireEmployeeAsync(ct);
        var claim = new ExpenseClaim
        {
            Number = await _numbers.NextAsync(Today.Year, ct),
            EmployeeId = me.Id,
            Employee = me,
            Purpose = request.Purpose.Trim(),
            CostCenter = string.IsNullOrWhiteSpace(request.CostCenter) ? me.CostCenter : request.CostCenter,
            ProjectCode = string.IsNullOrWhiteSpace(request.ProjectCode) ? null : request.ProjectCode,
            Status = ClaimStatus.Draft,
            CreatedAt = Now
        };
        AddEvent(claim, "Created");
        _db.Claims.Add(claim);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Claim {ClaimNumber} created by {Upn}", claim.Number, _user.Upn);
        return await ToDtoAsync(claim, ct);
    }

    public async Task<ClaimDto> UpdateAsync(int id, UpdateClaimRequest request, CancellationToken ct)
    {
        var claim = await LoadEditableAsync(id, ct);
        claim.Purpose = request.Purpose.Trim();
        if (!string.IsNullOrWhiteSpace(request.CostCenter))
            claim.CostCenter = request.CostCenter;
        claim.ProjectCode = string.IsNullOrWhiteSpace(request.ProjectCode) ? null : request.ProjectCode;
        await _db.SaveChangesAsync(ct);
        return await ToDtoAsync(claim, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var claim = await LoadAsync(id, ct);
        await RequireOwnerAsync(claim, ct);
        if (claim.Status != ClaimStatus.Draft)
            throw ClaimRuleException.Conflict("Only a draft claim can be deleted.");
        var receipts = claim.Lines.Where(l => l.Receipt is not null).Select(l => l.Receipt!).ToList();
        _db.Claims.Remove(claim);
        _db.Receipts.RemoveRange(receipts);
        await _db.SaveChangesAsync(ct);
        foreach (var receipt in receipts)
            _receipts.Delete(receipt.StoragePath);
    }

    public async Task<ClaimDto> AddLineAsync(int id, ClaimLineRequest request, CancellationToken ct)
    {
        var claim = await LoadEditableAsync(id, ct);
        var line = new ClaimLine { LineNumber = claim.Lines.Count == 0 ? 1 : claim.Lines.Max(l => l.LineNumber) + 1 };
        Apply(line, request);
        claim.Lines.Add(line);
        await RecalculateAsync(claim, ct);
        await _db.SaveChangesAsync(ct);
        return await ToDtoAsync(claim, ct);
    }

    public async Task<ClaimDto> UpdateLineAsync(int id, int lineId, ClaimLineRequest request, CancellationToken ct)
    {
        var claim = await LoadEditableAsync(id, ct);
        var line = claim.Lines.SingleOrDefault(l => l.Id == lineId) ?? throw ClaimRuleException.NotFound("Line");
        if (line.CardTransactionId is not null)
        {
            // Date, amount and currency come from the card company.
            request = request with
            {
                ExpenseDate = line.ExpenseDate,
                Amount = line.Amount,
                Currency = line.Currency,
                Category = request.Category is ExpenseCategory.Mileage or ExpenseCategory.PerDiem ? line.Category : request.Category
            };
        }
        Apply(line, request);
        await RecalculateAsync(claim, ct);
        await _db.SaveChangesAsync(ct);
        return await ToDtoAsync(claim, ct);
    }

    public async Task<ClaimDto> DeleteLineAsync(int id, int lineId, CancellationToken ct)
    {
        var claim = await LoadEditableAsync(id, ct);
        var line = claim.Lines.SingleOrDefault(l => l.Id == lineId) ?? throw ClaimRuleException.NotFound("Line");
        claim.Lines.Remove(line);
        var receipt = line.Receipt;
        if (receipt is not null)
            _db.Receipts.Remove(receipt);
        await RecalculateAsync(claim, ct);
        await _db.SaveChangesAsync(ct);
        if (receipt is not null)
            _receipts.Delete(receipt.StoragePath);
        return await ToDtoAsync(claim, ct);
    }

    public async Task<IReadOnlyList<CardTransactionDto>> ListOpenCardTransactionsAsync(CancellationToken ct)
    {
        var me = await RequireEmployeeAsync(ct);
        var used = _db.ClaimLines.Where(l => l.CardTransactionId != null).Select(l => l.CardTransactionId);
        var transactions = await _db.CardTransactions.AsNoTracking()
            .Include(t => t.CompanyCard)
            .Where(t => t.CompanyCard.EmployeeId == me.Id && !used.Contains(t.Id))
            .OrderBy(t => t.TransactionDate)
            .ToListAsync(ct);
        return transactions.Select(ToDto).ToList();
    }

    /// <summary>Adds a company card transaction to a claim as a line; the employee completes description and receipt.</summary>
    public async Task<ClaimDto> AddCardLineAsync(int id, int transactionId, CancellationToken ct)
    {
        var claim = await LoadEditableAsync(id, ct);
        var transaction = await _db.CardTransactions.Include(t => t.CompanyCard)
            .SingleOrDefaultAsync(t => t.Id == transactionId, ct) ?? throw ClaimRuleException.NotFound("Card transaction");
        if (transaction.CompanyCard.EmployeeId != claim.EmployeeId)
            throw ClaimRuleException.Forbidden("The card transaction belongs to another card holder.");
        if (await _db.ClaimLines.AnyAsync(l => l.CardTransactionId == transactionId, ct))
            throw ClaimRuleException.Conflict("The card transaction is already on a claim.");

        var line = new ClaimLine
        {
            LineNumber = claim.Lines.Count == 0 ? 1 : claim.Lines.Max(l => l.LineNumber) + 1,
            ExpenseDate = transaction.TransactionDate,
            Category = MerchantCategories.Suggest(transaction.MerchantCategoryCode),
            Description = transaction.Merchant,
            Amount = transaction.OriginalAmount,
            Currency = transaction.OriginalCurrency,
            Country = transaction.MerchantCountry is { Length: 2 } c ? c : null,
            PaidByCompany = true,
            CardTransactionId = transaction.Id,
            CardTransaction = transaction
        };
        claim.Lines.Add(line);
        await RecalculateAsync(claim, ct);
        await _db.SaveChangesAsync(ct);
        return await ToDtoAsync(claim, ct);
    }

    public async Task<ClaimDto> AttachReceiptAsync(int id, int lineId, IFormFile file, ReceiptStoreOptions options, CancellationToken ct)
    {
        var claim = await LoadEditableAsync(id, ct);
        var line = claim.Lines.SingleOrDefault(l => l.Id == lineId) ?? throw ClaimRuleException.NotFound("Line");
        if (file.Length == 0 || file.Length > options.MaxFileBytes)
            throw ClaimRuleException.Invalid($"A receipt must be between 1 byte and {options.MaxFileBytes / 1024 / 1024} MB.");
        if (!options.AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            throw ClaimRuleException.Invalid("Receipts can be PDF, JPEG, PNG or HEIC files.");

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || extension.Length > 5)
            extension = file.ContentType switch { "application/pdf" => ".pdf", "image/png" => ".png", "image/heic" => ".heic", _ => ".jpg" };

        await using var stream = file.OpenReadStream();
        var stored = await _receipts.SaveAsync(stream, extension, ct);
        var me = await RequireEmployeeAsync(ct);
        var previous = line.Receipt;
        line.Receipt = new Receipt
        {
            FileId = stored.FileId,
            FileName = Path.GetFileName(file.FileName),
            ContentType = file.ContentType,
            Length = stored.Length,
            Sha256 = stored.Sha256,
            StoragePath = stored.RelativePath,
            UploadedById = me.Id,
            UploadedAt = Now
        };
        if (previous is not null)
            _db.Receipts.Remove(previous);
        await _db.SaveChangesAsync(ct);
        if (previous is not null)
            _receipts.Delete(previous.StoragePath);
        return await ToDtoAsync(claim, ct);
    }

    public async Task<(Receipt Receipt, Stream Content)> OpenReceiptAsync(int receiptId, CancellationToken ct)
    {
        var line = await _db.ClaimLines.Include(l => l.Receipt).Include(l => l.Claim).ThenInclude(c => c.Approvals)
            .SingleOrDefaultAsync(l => l.ReceiptId == receiptId, ct) ?? throw ClaimRuleException.NotFound("Receipt");
        await RequireCanViewAsync(line.Claim, ct);
        return (line.Receipt!, _receipts.OpenRead(line.Receipt!.StoragePath));
    }

    public async Task<ClaimDto> SubmitAsync(int id, CancellationToken ct)
    {
        var claim = await LoadEditableAsync(id, ct);
        await RecalculateAsync(claim, ct);
        var findings = Check(claim);
        if (findings.Any(f => f.Severity == FindingSeverity.Error))
            throw ClaimRuleException.Invalid("The claim has errors: " + string.Join(" ", findings.Where(f => f.Severity == FindingSeverity.Error).Select(f => f.Message)));

        var managers = await _db.Employees.AsNoTracking().Where(e => e.Active)
            .Select(e => new { e.EmployeeNumber, e.ManagerEmployeeNumber }).ToListAsync(ct);
        var managerOf = managers.ToDictionary(e => e.EmployeeNumber, e => e.ManagerEmployeeNumber, StringComparer.OrdinalIgnoreCase);
        var chain = ApprovalRouting.Build(
            claim.Employee.EmployeeNumber,
            claim.Total,
            _policy.Options,
            n => managerOf.TryGetValue(n, out var m) && m is not null && managerOf.ContainsKey(m) ? m : null,
            _approval.FallbackApprover);
        if (chain.Count == 0)
            throw ClaimRuleException.Invalid("No approver was found. Ask HR to check your line manager.");

        var numbers = chain.Select(s => s.ApproverEmployeeNumber).ToList();
        var approvers = await _db.Employees.Where(e => numbers.Contains(e.EmployeeNumber)).ToDictionaryAsync(e => e.EmployeeNumber, ct);

        _db.Approvals.RemoveRange(claim.Approvals);
        claim.Approvals.Clear();
        foreach (var step in chain)
            claim.Approvals.Add(new ClaimApproval { Level = step.Level, Approver = approvers[step.ApproverEmployeeNumber], Decision = ApprovalDecision.Pending });

        claim.Status = ClaimStatus.Submitted;
        claim.SubmittedAt = Now;
        AddEvent(claim, "Submitted", $"{claim.Total:0.00} EUR, approvers {string.Join(", ", chain.Select(s => approvers[s.ApproverEmployeeNumber].DisplayName))}");
        await SaveAsync(ct);
        _logger.LogInformation("Claim {ClaimNumber} submitted, total {Total} EUR, {Levels} approval level(s)", claim.Number, claim.Total, chain.Count);
        return await ToDtoAsync(claim, ct);
    }

    public async Task<IReadOnlyList<ClaimSummaryDto>> ListPendingApprovalsAsync(CancellationToken ct)
    {
        var me = await RequireEmployeeAsync(ct);
        var claims = await _db.Claims.AsNoTracking()
            .Include(c => c.Employee)
            .Include(c => c.Approvals)
            .Where(c => c.Status == ClaimStatus.Submitted && c.Approvals.Any(a => a.ApproverId == me.Id && a.Decision == ApprovalDecision.Pending))
            .Select(c => new { c, LineCount = c.Lines.Count })
            .ToListAsync(ct);
        return claims
            .Where(x => CurrentStep(x.c)?.ApproverId == me.Id)
            .OrderBy(x => x.c.SubmittedAt)
            .Select(x => Summary(x.c, x.c.Employee.DisplayName, x.LineCount))
            .ToList();
    }

    public async Task<ClaimDto> DecideAsync(int id, ApprovalDecision decision, DecisionRequest request, CancellationToken ct)
    {
        var claim = await LoadAsync(id, ct);
        var me = await RequireEmployeeAsync(ct);
        if (claim.Status != ClaimStatus.Submitted)
            throw ClaimRuleException.Conflict($"Claim {claim.Number} is not waiting for approval.");
        var step = CurrentStep(claim);
        if (step is null || step.ApproverId != me.Id)
            throw ClaimRuleException.Forbidden($"You are not the current approver of claim {claim.Number}.");
        if (decision != ApprovalDecision.Approved && string.IsNullOrWhiteSpace(request.Comment))
            throw ClaimRuleException.Invalid("A comment is required when a claim is rejected or returned.");

        step.Decision = decision;
        step.Comment = request.Comment;
        step.DecidedAt = Now;

        switch (decision)
        {
            case ApprovalDecision.Approved when CurrentStep(claim) is null:
                claim.Status = ClaimStatus.Approved;
                claim.DecidedAt = Now;
                break;
            case ApprovalDecision.Rejected:
                claim.Status = ClaimStatus.Rejected;
                claim.DecidedAt = Now;
                break;
            case ApprovalDecision.Returned:
                claim.Status = ClaimStatus.Returned;
                break;
        }

        AddEvent(claim, decision.ToString(), request.Comment);
        await SaveAsync(ct);
        _logger.LogInformation("Claim {ClaimNumber} {Decision} at level {Level} by {Upn}", claim.Number, decision, step.Level, _user.Upn);
        return await ToDtoAsync(claim, ct);
    }

    public async Task<IReadOnlyList<ClaimSummaryDto>> ListForFinanceAsync(ClaimStatus status, CancellationToken ct)
    {
        RequireFinance();
        var claims = await _db.Claims.AsNoTracking()
            .Include(c => c.Employee)
            .Where(c => c.Status == status)
            .OrderBy(c => c.DecidedAt)
            .Select(c => new { c, LineCount = c.Lines.Count })
            .Take(500)
            .ToListAsync(ct);
        return claims.Select(x => Summary(x.c, x.c.Employee.DisplayName, x.LineCount)).ToList();
    }

    public async Task<ClaimDto> FinanceCheckAsync(int id, bool accept, DecisionRequest request, CancellationToken ct)
    {
        RequireFinance();
        var claim = await LoadAsync(id, ct);
        if (claim.Status != ClaimStatus.Approved)
            throw ClaimRuleException.Conflict($"Claim {claim.Number} is not approved.");
        if (string.Equals(claim.Employee.Upn, _user.Upn, StringComparison.OrdinalIgnoreCase))
            throw ClaimRuleException.Forbidden("You cannot check your own claim.");

        if (accept)
        {
            if (claim.Reimbursable > 0 && !Iban.IsValid(claim.Employee.Iban))
                throw ClaimRuleException.Invalid($"{claim.Employee.DisplayName} has no valid bank account in the HR system.");
            claim.Status = ClaimStatus.Checked;
            claim.CheckedAt = Now;
            claim.CheckedBy = _user.Upn;
            AddEvent(claim, "Checked", request.Comment);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Comment))
                throw ClaimRuleException.Invalid("A comment is required when a claim is returned.");
            claim.Status = ClaimStatus.Returned;
            AddEvent(claim, "ReturnedByFinance", request.Comment);
        }

        await SaveAsync(ct);
        return await ToDtoAsync(claim, ct);
    }

    public async Task<IReadOnlyList<PaymentRunDto>> ListPaymentRunsAsync(CancellationToken ct)
    {
        RequireFinance();
        var runs = await _db.PaymentRuns.AsNoTracking().OrderByDescending(r => r.CreatedAt).Take(100).ToListAsync(ct);
        return runs.Select(r => new PaymentRunDto(r.Id, r.MessageId, r.CreatedAt, r.ExecutionDate, r.PaymentCount, r.Total, r.Status, r.BankReference, r.Error)).ToList();
    }

    public IReadOnlyList<PolicyFinding> Check(ExpenseClaim claim)
    {
        var lines = claim.Lines.OrderBy(l => l.LineNumber).Select(ToInput).ToList();
        var amounts = claim.Lines.ToDictionary(l => l.LineNumber, l => l.AmountEur);
        var findings = _policy.Check(lines, amounts, Today).ToList();
        foreach (var line in claim.Lines.Where(l => l.ExchangeRate == 0m))
            findings.Add(new PolicyFinding(line.LineNumber, FindingSeverity.Error, "NO_RATE", $"No exchange rate for {line.Currency} on {line.ExpenseDate:d.M.yyyy}."));
        return findings;
    }

    private async Task RecalculateAsync(ExpenseClaim claim, CancellationToken ct)
    {
        var baseCurrency = _policy.Options.BaseCurrency;
        var foreign = claim.Lines.Where(l => !string.Equals(l.Currency, baseCurrency, StringComparison.OrdinalIgnoreCase)).ToList();
        var rates = new List<ReferenceRate>();
        if (foreign.Count > 0)
        {
            var currencies = foreign.Select(l => l.Currency).Distinct().ToList();
            var from = foreign.Min(l => l.ExpenseDate).AddDays(-CurrencyConverter.MaxRateAgeDays);
            var to = foreign.Max(l => l.ExpenseDate);
            rates = (await _db.ExchangeRates.AsNoTracking()
                    .Where(r => currencies.Contains(r.Currency) && r.Date >= from && r.Date <= to)
                    .ToListAsync(ct))
                .Select(r => new ReferenceRate(r.Date, r.Currency, r.UnitsPerEuro))
                .ToList();
        }

        var cardIds = claim.Lines.Where(l => l.CardTransactionId != null && l.CardTransaction == null).Select(l => l.CardTransactionId).ToList();
        if (cardIds.Count > 0)
            await _db.CardTransactions.Where(t => cardIds.Contains(t.Id)).LoadAsync(ct);

        foreach (var line in claim.Lines)
        {
            line.VatRate = _vat.RateFor(line.Category, line.Currency, line.Country);
            if (line.CardTransaction is not null)
            {
                // The card company's converted amount is what the company pays.
                line.AmountEur = line.CardTransaction.BilledAmountEur;
                line.ExchangeRate = line.AmountEur == 0 ? 1m : Math.Round(line.Amount / line.AmountEur, 6);
                line.VatAmountEur = VatRules.VatOf(line.AmountEur, line.VatRate);
                continue;
            }

            var amount = _policy.CalculateAmount(ToInput(line));
            if (line.Category is ExpenseCategory.Mileage or ExpenseCategory.PerDiem)
            {
                line.Amount = amount;
                line.Currency = baseCurrency;
            }

            if (string.Equals(line.Currency, baseCurrency, StringComparison.OrdinalIgnoreCase))
            {
                line.ExchangeRate = 1m;
                line.AmountEur = line.Amount;
                line.VatAmountEur = VatRules.VatOf(line.AmountEur, line.VatRate);
                continue;
            }

            var rate = CurrencyConverter.PickRate(rates, line.Currency, line.ExpenseDate);
            line.ExchangeRate = rate?.UnitsPerEuro ?? 0m;
            line.AmountEur = rate is null ? 0m : CurrencyConverter.ToEuro(line.Amount, rate.UnitsPerEuro);
            line.VatAmountEur = 0m;
        }

        claim.Total = claim.Lines.Sum(l => l.AmountEur);
        claim.Reimbursable = claim.Lines.Where(l => !l.PaidByCompany).Sum(l => l.AmountEur);
    }

    private static void Apply(ClaimLine line, ClaimLineRequest request)
    {
        line.ExpenseDate = request.ExpenseDate;
        line.Category = request.Category;
        line.Description = request.Description.Trim();
        line.Amount = Math.Round(request.Amount, 2);
        line.Currency = string.IsNullOrWhiteSpace(request.Currency) ? "EUR" : request.Currency.ToUpperInvariant();
        line.Kilometres = request.Category == ExpenseCategory.Mileage ? request.Kilometres : null;
        line.Route = request.Category == ExpenseCategory.Mileage ? request.Route : null;
        line.PerDiem = request.Category == ExpenseCategory.PerDiem ? request.PerDiem : PerDiemType.None;
        line.Country = string.IsNullOrWhiteSpace(request.Country) ? null : request.Country.ToUpperInvariant();
        line.Attendees = request.Attendees;
    }

    private static ClaimLineInput ToInput(ClaimLine l) => new(
        l.LineNumber, l.ExpenseDate, l.Category, l.Amount, l.Currency, l.Kilometres, l.PerDiem, l.Country, l.Attendees,
        l.ReceiptId is not null || l.Receipt is not null, l.Description);

    private static ClaimApproval? CurrentStep(ExpenseClaim claim) =>
        claim.Approvals.Where(a => a.Decision == ApprovalDecision.Pending).OrderBy(a => a.Level).FirstOrDefault();

    private void AddEvent(ExpenseClaim claim, string action, string? details = null) =>
        claim.Events.Add(new ClaimEvent { At = Now, Actor = _user.Upn, Action = action, Details = details });

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ClaimRuleException.Conflict("The claim was changed by someone else. Reload and try again.");
        }
    }

    private async Task<ExpenseClaim> LoadAsync(int id, CancellationToken ct) =>
        await _db.Claims
            .Include(c => c.Employee)
            .Include(c => c.Lines).ThenInclude(l => l.Receipt)
            .Include(c => c.Lines).ThenInclude(l => l.CardTransaction)
            .Include(c => c.Approvals).ThenInclude(a => a.Approver)
            .Include(c => c.Events)
            .AsSplitQuery()
            .SingleOrDefaultAsync(c => c.Id == id, ct)
        ?? throw ClaimRuleException.NotFound();

    private async Task<ExpenseClaim> LoadEditableAsync(int id, CancellationToken ct)
    {
        var claim = await LoadAsync(id, ct);
        await RequireOwnerAsync(claim, ct);
        if (claim.Status is not (ClaimStatus.Draft or ClaimStatus.Returned))
            throw ClaimRuleException.Conflict($"Claim {claim.Number} is {claim.Status} and cannot be changed.");
        return claim;
    }

    private async Task<Employee> RequireEmployeeAsync(CancellationToken ct) =>
        await _user.GetEmployeeAsync(ct)
        ?? throw ClaimRuleException.Forbidden($"{_user.Upn} is not in the employee register. New employees are added by the nightly HR import.");

    private async Task RequireOwnerAsync(ExpenseClaim claim, CancellationToken ct)
    {
        var me = await RequireEmployeeAsync(ct);
        if (claim.EmployeeId != me.Id)
            throw ClaimRuleException.Forbidden("Only the claimant can change the claim.");
    }

    private async Task RequireCanViewAsync(ExpenseClaim claim, CancellationToken ct)
    {
        if (_user.IsFinance)
            return;
        var me = await RequireEmployeeAsync(ct);
        if (claim.EmployeeId != me.Id && claim.Approvals.All(a => a.ApproverId != me.Id))
            throw ClaimRuleException.NotFound();
    }

    private void RequireFinance()
    {
        if (!_user.IsFinance)
            throw ClaimRuleException.Forbidden("Finance role is required.");
    }

    private static CardTransactionDto ToDto(CardTransaction t) => new(
        t.Id, t.CompanyCard?.MaskedNumber ?? "", t.TransactionDate, t.Merchant, t.MerchantCity, t.MerchantCountry,
        t.MerchantCategoryCode, MerchantCategories.Suggest(t.MerchantCategoryCode), t.OriginalAmount, t.OriginalCurrency, t.BilledAmountEur);

    private static ClaimSummaryDto Summary(ExpenseClaim c, string employee, int lineCount) =>
        new(c.Id, c.Number, employee, c.Purpose, c.Status, c.Total, c.CreatedAt, c.SubmittedAt, lineCount);

    private async Task<ClaimDto> ToDtoAsync(ExpenseClaim c, CancellationToken ct)
    {
        var me = await _user.GetEmployeeAsync(ct);
        var isOwner = me is not null && c.EmployeeId == me.Id;
        var step = CurrentStep(c);
        return new ClaimDto(
            c.Id,
            c.Number,
            c.Employee.EmployeeNumber,
            c.Employee.DisplayName,
            c.Purpose,
            c.CostCenter,
            c.ProjectCode,
            c.Status,
            c.Total,
            c.Reimbursable,
            c.CreatedAt,
            c.SubmittedAt,
            c.PaidAt,
            Convert.ToBase64String(c.RowVersion),
            isOwner && c.Status is ClaimStatus.Draft or ClaimStatus.Returned,
            me is not null && c.Status == ClaimStatus.Submitted && step?.ApproverId == me.Id,
            c.Lines.OrderBy(l => l.LineNumber).Select(l => new ClaimLineDto(
                l.Id, l.LineNumber, l.ExpenseDate, l.Category, l.Description, l.Amount, l.Currency, l.ExchangeRate, l.AmountEur,
                l.Kilometres, l.Route, l.PerDiem, l.Country, l.Attendees, l.VatRate, l.VatAmountEur, l.PaidByCompany,
                l.CardTransaction is null ? null : ToDto(l.CardTransaction),
                l.Receipt is null ? null : new ReceiptDto(l.Receipt.Id, l.Receipt.FileName, l.Receipt.ContentType, l.Receipt.Length))).ToList(),
            c.Approvals.OrderBy(a => a.Level).Select(a => new ApprovalDto(a.Level, a.Approver.DisplayName, a.Decision, a.Comment, a.DecidedAt)).ToList(),
            Check(c),
            c.Events.OrderBy(e => e.At).ThenBy(e => e.Id).Select(e => new ClaimEventDto(e.At, e.Actor, e.Action, e.Details)).ToList());
    }
}

public sealed class ApprovalOptions
{
    public const string SectionName = "Approval";

    /// <summary>Employee number of the approver used when the claimant has no manager (for example the CEO).</summary>
    public string? FallbackApprover { get; set; }
}
