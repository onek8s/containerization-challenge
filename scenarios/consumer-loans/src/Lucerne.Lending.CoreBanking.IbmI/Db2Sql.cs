using System.Data;
using System.Data.Odbc;
using System.Data.OleDb;
using System.Runtime.Versioning;
using Microsoft.Extensions.Options;

namespace Lucerne.Lending.CoreBanking.IbmI;

/// <summary>SQL access to DB2 for i through the IBM i Access ODBC Driver (system naming, *NONE commit).</summary>
internal sealed class OdbcCore
{
    private readonly CoreOptions _options;

    public OdbcCore(IOptions<CoreOptions> options) => _options = options.Value;

    public async Task<List<T>> QueryAsync<T>(string sql, Action<OdbcParameterCollection> bind, Func<IDataRecord, T> map, CancellationToken ct)
    {
        await using var cn = new OdbcConnection(_options.OdbcConnectionString);
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = _options.ProgramCallTimeoutSeconds;
        bind(cmd.Parameters);
        var list = new List<T>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) list.Add(map(reader));
        return list;
    }

    /// <summary>Calls an SQL procedure that returns one result set ({CALL LIB/PROC(?, ?)}).</summary>
    public Task<List<T>> CallAsync<T>(string procedure, object?[] args, Func<IDataRecord, T> map, CancellationToken ct)
    {
        var marks = string.Join(", ", args.Select(_ => "?"));
        return QueryAsync("{CALL " + _options.LendingLibrary + "/" + procedure + "(" + marks + ")}", p =>
        {
            foreach (var a in args) p.AddWithValue("@p" + p.Count, a ?? DBNull.Value);
        }, map, ct);
    }
}

/// <summary>Loan transaction history through the IBMDA400 OLE DB provider.</summary>
[SupportedOSPlatform("windows")]
internal sealed class OleDbHistory
{
    private readonly CoreOptions _options;

    public OleDbHistory(IOptions<CoreOptions> options) => _options = options.Value;

    public async Task<List<CorePayment>> ReadAsync(string accountNumber, DateOnly from, CancellationToken ct)
    {
        await using var cn = new OleDbConnection(_options.OleDbConnectionString);
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        // LNTRNP: loan transaction physical file (DDS), dates stored as 8,0 numeric CCYYMMDD
        cmd.CommandText = "SELECT TRBKDT, TRTYPE, TRAMT, TRPRIN, TRINT, TRFEES, TRBALA FROM " + _options.LendingLibrary +
                          ".LNTRNP WHERE TRACCT = ? AND TRBKDT >= ? AND TRRVSD <> 'Y' ORDER BY TRBKDT, TRSEQN";
        cmd.Parameters.Add("@acct", OleDbType.Char, 12).Value = accountNumber.PadRight(12);
        cmd.Parameters.Add("@from", OleDbType.Numeric).Value = decimal.Parse(from.ToString("yyyyMMdd"));
        var list = new List<CorePayment>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var date = DateOnly.ParseExact(Convert.ToInt64(r.GetDecimal(0)).ToString(), "yyyyMMdd");
            list.Add(new CorePayment(date, TransactionType(r.GetString(1).Trim()), r.GetDecimal(2), r.GetDecimal(3),
                r.GetDecimal(4), r.GetDecimal(5), r.GetDecimal(6)));
        }
        return list;
    }

    private static string TransactionType(string code) => code switch
    {
        "DB" => "Disbursement",
        "PM" => "Payment",
        "IN" => "Interest",
        "FE" => "Fee",
        "RF" => "ReminderFee",
        "AD" => "Adjustment",
        "TM" => "Termination",
        _ => code
    };
}
