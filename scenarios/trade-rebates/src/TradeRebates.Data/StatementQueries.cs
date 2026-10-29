using System.Data;
using ContosoFoods.TradeRebates.Core;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ContosoFoods.TradeRebates.Data;

/// <summary>Statement calls shared by the portal and the statement worker (which runs them inside its receive transaction).</summary>
public static class StatementQueries
{
    public static async Task<StatementDocument?> GetAsync(SqlConnection c, SqlTransaction? tx, int statementId, int timeout)
    {
        using var r = await c.QueryMultipleAsync("rebate.GetStatement", new { StatementId = statementId }, tx,
            commandType: CommandType.StoredProcedure, commandTimeout: timeout);
        var header = await r.ReadSingleOrDefaultAsync<StatementHeader>();
        if (header is null)
            return null;
        return new StatementDocument { Header = header, Lines = (await r.ReadAsync<StatementLine>()).ToList() };
    }

    public static async Task<string?> CompleteAsync(SqlConnection c, SqlTransaction tx, int statementId, string filePath, string actor, int timeout)
    {
        var row = await c.QuerySingleAsync("rebate.CompleteStatement", new { StatementId = statementId, FilePath = filePath, Actor = actor }, tx,
            commandType: CommandType.StoredProcedure, commandTimeout: timeout);
        return (string?)row.ErpDocumentNo;
    }

    public static Task FailAsync(SqlConnection c, SqlTransaction tx, int statementId, string error, string actor, int timeout) =>
        c.ExecuteAsync("rebate.FailStatement", new { StatementId = statementId, Error = error, Actor = actor }, tx,
            commandType: CommandType.StoredProcedure, commandTimeout: timeout);
}
