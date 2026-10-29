using System.Data;
using ContosoFoods.TradeRebates.Core;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ContosoFoods.TradeRebates.Data;

public sealed class RebateDatabase(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("TradeRebates")
        ?? throw new InvalidOperationException("Connection string 'TradeRebates' is missing.");

    public int CommandTimeoutSeconds { get; } = configuration.GetValue("Database:CommandTimeoutSeconds", 60);

    public SqlConnection Open()
    {
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public async Task<SqlConnection> OpenAsync(CancellationToken ct = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    /// <summary>Runs a database call and turns business rule errors (THROW 50000-50999) into <see cref="RebateRuleException"/>.</summary>
    public static async Task<T> Rules<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (SqlException ex) when (ex.Number is >= 50000 and < 51000)
        {
            throw new RebateRuleException(ex.Number, ex.Message);
        }
    }

    public static async Task Rules(Func<Task> call)
    {
        try
        {
            await call();
        }
        catch (SqlException ex) when (ex.Number is >= 50000 and < 51000)
        {
            throw new RebateRuleException(ex.Number, ex.Message);
        }
    }

    public static DataTable ClaimLineTable(IEnumerable<ClaimLineInput> lines)
    {
        var table = new DataTable();
        table.Columns.Add("LineNumber", typeof(int));
        table.Columns.Add("InvoiceNo", typeof(string));
        table.Columns.Add("InvoiceDate", typeof(DateTime));
        table.Columns.Add("CustomerNo", typeof(string));
        table.Columns.Add("Gtin", typeof(string));
        table.Columns.Add("Quantity", typeof(decimal));
        table.Columns.Add("NetAmount", typeof(decimal));
        int n = 0;
        foreach (ClaimLineInput l in lines)
            table.Rows.Add(++n, l.InvoiceNo, l.InvoiceDate.Date, l.CustomerNo ?? "", l.Gtin, l.Quantity, l.NetAmount);
        return table;
    }
}
