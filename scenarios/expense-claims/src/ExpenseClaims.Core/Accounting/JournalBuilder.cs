using ExpenseClaims.Core.Claims;

namespace ExpenseClaims.Core.Accounting;

public sealed record JournalClaim(
    string Number,
    string EmployeeNumber,
    string EmployeeName,
    string CostCenter,
    string? ProjectCode,
    DateOnly PostingDate,
    IReadOnlyList<JournalClaimLine> Lines);

public sealed record JournalClaimLine(
    int LineNumber,
    ExpenseCategory Category,
    string Description,
    decimal AmountEur,
    decimal VatAmountEur,
    bool PaidByCompany);

public sealed record JournalLine(
    int LineNo,
    string DocumentNo,
    DateOnly PostingDate,
    string AccountNo,
    string Description,
    decimal Amount,
    string? CostCenter,
    string? Project,
    string? EmployeeNumber);

/// <summary>
/// Builds general journal lines for a claim: one debit per expense line (net of VAT) with cost center and
/// project dimensions, one debit for deductible VAT and credits to the employee payable and card clearing accounts.
/// Debits are positive, credits negative; the lines of each document balance to zero.
/// </summary>
public sealed class JournalBuilder
{
    private readonly AccountingOptions _options;

    public JournalBuilder(AccountingOptions options) => _options = options;

    public IReadOnlyList<JournalLine> Build(JournalClaim claim)
    {
        var lines = new List<JournalLine>();
        var lineNo = 0;
        JournalLine Line(string account, string text, decimal amount, bool dimensions, string? employee = null) =>
            new(lineNo += 10000, claim.Number, claim.PostingDate, account, Text(text), amount,
                dimensions ? claim.CostCenter : null, dimensions ? claim.ProjectCode : null, employee);

        foreach (var l in claim.Lines.OrderBy(l => l.LineNumber))
        {
            if (!_options.ExpenseAccounts.TryGetValue(l.Category.ToString(), out var account))
                throw new InvalidOperationException($"No expense account for category {l.Category}.");
            lines.Add(Line(account, $"{claim.Number} {l.Description}", l.AmountEur - l.VatAmountEur, dimensions: true));
        }

        var vat = claim.Lines.Sum(l => l.VatAmountEur);
        if (vat != 0)
            lines.Add(Line(_options.VatReceivableAccount, $"{claim.Number} VAT", vat, dimensions: false));

        var employee = claim.Lines.Where(l => !l.PaidByCompany).Sum(l => l.AmountEur);
        if (employee != 0)
            lines.Add(Line(_options.EmployeePayableAccount, $"{claim.Number} {claim.EmployeeName}", -employee, dimensions: false, claim.EmployeeNumber));

        var card = claim.Lines.Where(l => l.PaidByCompany).Sum(l => l.AmountEur);
        if (card != 0)
            lines.Add(Line(_options.CardClearingAccount, $"{claim.Number} company card {claim.EmployeeName}", -card, dimensions: false, claim.EmployeeNumber));

        return lines;
    }

    private static string Text(string value) => value.Length <= 50 ? value : value[..50];
}
