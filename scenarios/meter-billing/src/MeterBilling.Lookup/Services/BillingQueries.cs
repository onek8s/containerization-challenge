using Dapper;
using Microsoft.Data.SqlClient;

namespace Coho.Billing.Lookup.Services;

public class BillingQueries
{
    private readonly string _connectionString;

    public BillingQueries(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MeterBilling")
            ?? throw new InvalidOperationException("Connection string 'MeterBilling' is missing");
    }

    private SqlConnection Connect() => new SqlConnection(_connectionString);

    public async Task<IReadOnlyList<AccountHit>> SearchAsync(string text)
    {
        text = text.Trim();
        using var connection = Connect();

        // Bill numbers are 12 digits; account numbers 8.
        if (text.Length == 12 && text.All(char.IsDigit))
        {
            return (await connection.QueryAsync<AccountHit>(@"
SELECT a.AccountNumber, a.CustomerName, a.AddressLine1, a.City, a.MeterSerial, a.Active
FROM bill.Bill b JOIN bill.Account a ON a.AccountNumber = b.AccountNumber
WHERE b.BillNumber = @text", new { text })).ToList();
        }

        return (await connection.QueryAsync<AccountHit>(@"
SELECT TOP (50) AccountNumber, CustomerName, AddressLine1, City, MeterSerial, Active
FROM bill.Account
WHERE AccountNumber = @text OR MeterSerial = @text OR CustomerName LIKE @like OR AddressLine1 LIKE @like
ORDER BY Active DESC, CustomerName", new { text, like = "%" + text.Replace("[", "[[]").Replace("%", "[%]") + "%" })).ToList();
    }

    public async Task<AccountView> GetAccountAsync(string accountNumber)
    {
        using var connection = Connect();
        return await connection.QuerySingleOrDefaultAsync<AccountView>(@"
SELECT a.*, m.SizeCode, c.Name AS CycleName, c.NextCutOff,
       (SELECT TOP (1) t.Name FROM bill.Tariff t WHERE t.Code = a.TariffCode ORDER BY t.ValidFrom DESC) AS TariffName
FROM bill.Account a
JOIN bill.Meter m ON m.Serial = a.MeterSerial
JOIN bill.BillCycle c ON c.CycleCode = a.CycleCode
WHERE a.AccountNumber = @accountNumber", new { accountNumber });
    }

    public async Task<IReadOnlyList<ReadView>> GetReadsAsync(string meterSerial)
    {
        using var connection = Connect();
        return (await connection.QueryAsync<ReadView>(@"
SELECT TOP (20) ReadDate, Reading, ReadType, Source, ReaderCode, Status
FROM bill.MeterRead WHERE MeterSerial = @meterSerial ORDER BY ReadDate DESC, ReadId DESC", new { meterSerial })).ToList();
    }

    public async Task<IReadOnlyList<BillView>> GetBillsAsync(string accountNumber)
    {
        using var connection = Connect();
        return (await connection.QueryAsync<BillView>(@"
SELECT TOP (24) * FROM bill.Bill WHERE AccountNumber = @accountNumber AND Status <> 0 ORDER BY BillDate DESC, BillId DESC", new { accountNumber })).ToList();
    }

    public async Task<(BillView Bill, IReadOnlyList<BillLineView> Lines, IReadOnlyList<ReprintView> Reprints)> GetBillAsync(string billNumber)
    {
        using var connection = Connect();
        var bill = await connection.QuerySingleOrDefaultAsync<BillView>("SELECT * FROM bill.Bill WHERE BillNumber = @billNumber", new { billNumber });
        if (bill == null)
        {
            return (null, Array.Empty<BillLineView>(), Array.Empty<ReprintView>());
        }

        var lines = (await connection.QueryAsync<BillLineView>("SELECT * FROM bill.BillLine WHERE BillId = @BillId ORDER BY LineNumber", new { bill.BillId })).ToList();
        var reprints = (await connection.QueryAsync<ReprintView>(
            "SELECT RequestedAt, RequestedBy, Reason, ProcessedAt FROM bill.ReprintRequest WHERE BillNumber = @billNumber ORDER BY RequestId DESC", new { billNumber })).ToList();
        return (bill, lines, reprints);
    }
}
