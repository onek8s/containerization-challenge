using System.Data;
using System.Runtime.Versioning;
using Microsoft.Extensions.Options;

namespace Lucerne.Lending.CoreBanking.IbmI;

[SupportedOSPlatform("windows")]
internal sealed class IbmICustomerService : ICoreCustomerService
{
    private readonly ProgramCaller _programs;
    private readonly OdbcCore _sql;
    private readonly CoreOptions _options;

    public IbmICustomerService(ProgramCaller programs, OdbcCore sql, IOptions<CoreOptions> options)
    {
        _programs = programs;
        _sql = sql;
        _options = options.Value;
    }

    public Task<CoreCustomer?> FindByPersonalIdentityCodeAsync(string personalIdentityCode, CancellationToken ct = default) =>
        InquireAsync(personalIdentityCode.ToUpperInvariant(), "", ct);

    public Task<CoreCustomer?> GetAsync(string customerNumber, CancellationToken ct = default) =>
        InquireAsync("", customerNumber, ct);

    private async Task<CoreCustomer?> InquireAsync(string hetu, string cif, CancellationToken ct)
    {
        var f = await _programs.CallAsync("CUSTINQ", CoreLayouts.CustInqIn,
            new Dictionary<string, object?> { ["HETU"] = hetu, ["CIF"] = cif },
            CoreLayouts.CustInqOut, ct, _options.CustomerLibrary);
        if (f["RC"] == CoreReturnCodes.NotFound) return null;
        if (f["RC"] != CoreReturnCodes.Ok)
            throw new CoreBankingException($"CUSTINQ: {CoreReturnCodes.Describe(f["RC"])} {f["MSG"]}") { ReturnCode = f["RC"] };
        return new CoreCustomer(f["CIF"], f["HETU"], f["NAME"], f["STREET"], f["POSTAL"], f["CITY"], f["SEGMENT"], f["KYC"],
            f.Date("SINCE") ?? DateOnly.MinValue, f["RATING"], f.Flag("DECEASED"), f.Flag("BLOCKED"), f["BRANCH"]);
    }

    public async Task<IReadOnlyList<CoreAccount>> GetAccountsAsync(string customerNumber, CancellationToken ct = default) =>
        await _sql.QueryAsync(
            "SELECT A.ACACCT, A.ACIBAN, A.ACPROD, A.ACTYPE, A.ACBAL, R.ARROLE FROM CIFLIB/ACCREL R " +
            "JOIN DPLIB/ACMAST A ON A.ACACCT = R.ARACCT WHERE R.ARCIF = ? AND A.ACSTAT = 'A' ORDER BY A.ACOPDT",
            p => p.AddWithValue("@cif", customerNumber.PadRight(10)),
            r => new CoreAccount(r.GetString(0).Trim(), r.GetString(1).Trim(), r.GetString(2).Trim(), r.GetString(3).Trim(),
                r.GetDecimal(4), r.GetString(5).Trim()), ct);

    public async Task<CoreExposure> GetExposureAsync(string customerNumber, CancellationToken ct = default)
    {
        var loans = await _sql.CallAsync("LN_CUSTOMER_LOANS", new object?[] { customerNumber }, MapLoan, ct);
        var totals = await _sql.CallAsync("LN_EXPOSURE_SUMMARY", new object?[] { customerNumber },
            r => (Limit: r.GetDecimal(0), Balance: r.GetDecimal(1), Arrears: r.GetDecimal(2), MaxDpd: r.GetInt32(3),
                Active: r.GetInt32(4), Monthly: r.GetDecimal(5)), ct);
        var t = totals.FirstOrDefault();
        return new CoreExposure(customerNumber, t.Limit, t.Balance, t.Arrears, t.MaxDpd, t.Active, t.Monthly, loans);
    }

    internal static CoreLoanSummary MapLoan(IDataRecord r)
    {
        var next = r.IsDBNull(5) ? (DateOnly?)null : DateOnly.FromDateTime(r.GetDateTime(5));
        return new CoreLoanSummary(r.GetString(0).Trim(), r.GetString(1).Trim(), r.GetDecimal(2), r.GetDecimal(3), r.GetDecimal(4),
            next, r.GetDecimal(6), r.GetDecimal(7), r.GetInt32(8), r.GetString(9).Trim(),
            r.FieldCount > 10 ? r.GetString(10).Trim() : "");
    }

    public async Task<string> CreateCustomerAsync(NewCustomerRequest request, CancellationToken ct = default)
    {
        var f = await _programs.CallAsync("CUSCRT", CoreLayouts.CusCrtIn, new Dictionary<string, object?>
        {
            ["HETU"] = request.PersonalIdentityCode.ToUpperInvariant(),
            ["FNAME"] = request.FirstName,
            ["LNAME"] = request.LastName,
            ["STREET"] = request.Street,
            ["POSTAL"] = request.PostalCode,
            ["CITY"] = request.City,
            ["CITIZEN"] = request.Citizenship,
            ["EMAIL"] = request.Email,
            ["PHONE"] = request.Phone,
            ["BRANCH"] = request.BranchCode,
            ["OFFICER"] = request.Officer
        }, CoreLayouts.CusCrtOut, ct, _options.CustomerLibrary);
        if (f["RC"] is CoreReturnCodes.Ok or CoreReturnCodes.Duplicate) return f["CIF"];
        throw new CoreBankingException($"CUSCRT: {CoreReturnCodes.Describe(f["RC"])} {f["MSG"]}") { ReturnCode = f["RC"] };
    }
}
