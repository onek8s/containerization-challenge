using System.Runtime.Versioning;
using Microsoft.Extensions.Options;

namespace Lucerne.Lending.CoreBanking.IbmI;

[SupportedOSPlatform("windows")]
internal sealed class IbmILoanService : ICoreLoanService
{
    private readonly ProgramCaller _programs;
    private readonly OdbcCore _sql;
    private readonly OleDbHistory _history;
    private readonly CoreOptions _options;

    public IbmILoanService(ProgramCaller programs, OdbcCore sql, OleDbHistory history, IOptions<CoreOptions> options)
    {
        _programs = programs;
        _sql = sql;
        _history = history;
        _options = options.Value;
    }

    public async Task<LoanBookingResult> BookAsync(LoanBookingRequest r, CancellationToken ct = default)
    {
        var f = await _programs.CallAsync("LNBOOK", CoreLayouts.LnBookIn, new Dictionary<string, object?>
        {
            ["APPREF"] = r.ApplicationReference,
            ["CIF"] = r.CustomerNumber,
            ["COCIF"] = r.CoBorrowerNumber ?? "",
            ["PRODUCT"] = r.CoreProductCode,
            ["AMOUNT"] = r.Amount,
            ["TERM"] = r.TermMonths,
            ["REFRATE"] = r.ReferenceRateCode,
            ["MARGIN"] = r.MarginPercent,
            ["RATE"] = r.NominalRatePercent,
            ["ARRFEE"] = r.ArrangementFee,
            ["MTHFEE"] = r.MonthlyFee,
            ["DUEDAY"] = r.DueDay,
            ["FIRSTDUE"] = r.FirstDueDate,
            ["BRANCH"] = r.BranchCode,
            ["OFFICER"] = r.Officer
        }, CoreLayouts.LnBookOut, ct);
        // LNBOOK returns DU with the existing account when the application reference was booked before
        var ok = f["RC"] is CoreReturnCodes.Ok or CoreReturnCodes.Duplicate;
        return new LoanBookingResult(ok, ok ? f["ACCOUNT"] : null, f["RC"], f["MSG"]);
    }

    public async Task<DisbursementResult> DisburseAsync(DisbursementRequest r, CancellationToken ct = default)
    {
        var f = await _programs.CallAsync("LNDISB", CoreLayouts.LnDisbIn, new Dictionary<string, object?>
        {
            ["ACCOUNT"] = r.AccountNumber,
            ["AMOUNT"] = r.Amount,
            ["IBAN"] = r.TargetIban,
            ["TEXT"] = r.ReferenceText,
            ["OFFICER"] = r.Officer
        }, CoreLayouts.LnDisbOut, ct);
        var ok = f["RC"] == CoreReturnCodes.Ok;
        return new DisbursementResult(ok, ok ? f["TXNID"] : null, f["EXTERNAL"] == "Y", f["RC"], f["MSG"]);
    }

    public async Task<CoreLoanSummary?> GetLoanAsync(string accountNumber, CancellationToken ct = default) =>
        (await _sql.CallAsync("LN_LOAN_STATUS", new object?[] { accountNumber }, IbmICustomerService.MapLoan, ct)).FirstOrDefault();

    public async Task<IReadOnlyList<CoreLoanSummary>> ListLoansAsync(string customerNumber, CancellationToken ct = default) =>
        await _sql.CallAsync("LN_CUSTOMER_LOANS", new object?[] { customerNumber }, IbmICustomerService.MapLoan, ct);

    public async Task<IReadOnlyList<CorePayment>> GetPaymentHistoryAsync(string accountNumber, DateOnly from, CancellationToken ct = default) =>
        await _history.ReadAsync(accountNumber, from, ct);

    public async Task<CoreOperationResult> ChargeFeeAsync(string accountNumber, string feeCode, decimal amount, string text, CancellationToken ct = default)
    {
        var f = await _programs.CallAsync("LNFEE", CoreLayouts.LnFeeIn, new Dictionary<string, object?>
        {
            ["ACCOUNT"] = accountNumber,
            ["FEECODE"] = feeCode,
            ["AMOUNT"] = amount,
            ["TEXT"] = text
        }, CoreLayouts.ResultOut, ct);
        return new CoreOperationResult(f["RC"] == CoreReturnCodes.Ok, f["RC"], f["MSG"]);
    }

    public async Task<CoreOperationResult> SetPaymentPlanAsync(string accountNumber, IReadOnlyList<PlanInstalmentRequest> instalments,
        string officer, CancellationToken ct = default)
    {
        if (instalments.Count is < 1 or > 12) throw new ArgumentException("1-12 instalments", nameof(instalments));
        var values = new Dictionary<string, object?>
        {
            ["ACCOUNT"] = accountNumber,
            ["OFFICER"] = officer,
            ["COUNT"] = instalments.Count
        };
        for (var i = 0; i < 12; i++)
        {
            values[$"DUE{i + 1:00}"] = i < instalments.Count ? instalments[i].DueDate : 0;
            values[$"AMT{i + 1:00}"] = i < instalments.Count ? instalments[i].Amount : 0m;
        }
        var f = await _programs.CallAsync("LNPPLN", CoreLayouts.LnPplnIn, values, CoreLayouts.ResultOut, ct);
        return new CoreOperationResult(f["RC"] == CoreReturnCodes.Ok, f["RC"], f["MSG"]);
    }

    public async Task<CoreOperationResult> TerminateAsync(string accountNumber, DateOnly effectiveDate, string officer, CancellationToken ct = default)
    {
        var f = await _programs.CallAsync("LNTERM", CoreLayouts.LnTermIn, new Dictionary<string, object?>
        {
            ["ACCOUNT"] = accountNumber,
            ["EFFDATE"] = effectiveDate,
            ["OFFICER"] = officer
        }, CoreLayouts.ResultOut, ct);
        return new CoreOperationResult(f["RC"] == CoreReturnCodes.Ok, f["RC"], f["MSG"]);
    }
}
