using System.Data;

namespace Lucerne.Lending.CoreBanking.IbmI;

internal sealed class IbmIReferenceData : ICoreReferenceData
{
    private readonly OdbcCore _sql;

    public IbmIReferenceData(OdbcCore sql) => _sql = sql;

    public async Task<decimal> GetReferenceRateAsync(string code, DateOnly date, CancellationToken ct = default)
    {
        if (code == "FIX") return 0m;
        var rows = await _sql.QueryAsync(
            "SELECT RRRATE FROM RATLIB/REFRATE WHERE RRCODE = ? AND RRVLDT <= ? ORDER BY RRVLDT DESC FETCH FIRST 1 ROW ONLY",
            p =>
            {
                p.AddWithValue("@code", code.PadRight(3));
                p.AddWithValue("@date", decimal.Parse(date.ToString("yyyyMMdd")));
            },
            r => r.GetDecimal(0), ct);
        return rows.Count == 0 ? throw new CoreBankingException($"No reference rate {code} on {date}.") : rows[0];
    }

    public async Task<DateOnly> GetBusinessDateAsync(CancellationToken ct = default)
    {
        // system value of the current core business date, maintained by end of day
        var rows = await _sql.QueryAsync("SELECT BDDATE FROM LNLIB/BUSDATE", _ => { },
            (IDataRecord r) => DateOnly.ParseExact(((long)r.GetDecimal(0)).ToString(), "yyyyMMdd"), ct);
        return rows.Count == 0 ? DateOnly.FromDateTime(DateTime.Today) : rows[0];
    }
}
