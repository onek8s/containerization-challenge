using System.Data;
using System.Xml.Linq;
using ContosoFoods.TradeRebates.Core;
using ContosoFoods.TradeRebates.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContosoFoods.TradeRebates.StatementWorker;

public sealed class StatementQueueReader(
    RebateDatabase db,
    IStatementFileWriter writer,
    IOptions<StatementOptions> options,
    ILogger<StatementQueueReader> log)
{
    private const string RequestType = "//contosofoods.com/TradeRebates/StatementRequest";
    private const string EndDialogType = "http://schemas.microsoft.com/SQL/ServiceBroker/EndDialog";
    private const string ErrorType = "http://schemas.microsoft.com/SQL/ServiceBroker/Error";
    private const string Actor = "StatementWorker";

    private sealed class Message
    {
        public Guid ConversationHandle { get; set; }
        public string MessageTypeName { get; set; } = "";
        public string? Body { get; set; }
    }

    static StatementQueueReader() => System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

    public async Task<RunSummary> DrainAsync(CancellationToken ct)
    {
        int completed = 0, failed = 0, other = 0;
        await using SqlConnection c = await db.OpenAsync(ct);

        for (int i = 0; i < options.Value.MaxMessagesPerRun && !ct.IsCancellationRequested; i++)
        {
            await using SqlTransaction tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
            Message? m = await ReceiveAsync(c, tx);
            if (m is null)
            {
                await tx.CommitAsync(ct);
                break;
            }

            if (m.MessageTypeName != RequestType)
            {
                await EndConversationAsync(c, tx, m.ConversationHandle);
                await tx.CommitAsync(ct);
                other++;
                continue;
            }

            int statementId = ParseStatementId(m.Body);
            try
            {
                await ProcessAsync(c, tx, statementId, m.ConversationHandle);
                await tx.CommitAsync(ct);
                completed++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Statement {StatementId} failed.", statementId);
                await tx.RollbackAsync(CancellationToken.None);
                await FailAsync(c, m.ConversationHandle, statementId, ex.Message);
                failed++;
            }
        }

        return new RunSummary(completed, failed, other);
    }

    private async Task<Message?> ReceiveAsync(SqlConnection c, SqlTransaction tx)
    {
        int timeoutMs = Math.Max(0, options.Value.ReceiveTimeoutSeconds) * 1000;
        return await c.QuerySingleOrDefaultAsync<Message>(
            @"WAITFOR (
                RECEIVE TOP (1) conversation_handle AS ConversationHandle, message_type_name AS MessageTypeName,
                       CAST(CAST(message_body AS xml) AS nvarchar(max)) AS Body
                FROM rebate.StatementRequestQueue
            ), TIMEOUT @timeoutMs;",
            new { timeoutMs }, tx, commandTimeout: options.Value.ReceiveTimeoutSeconds + db.CommandTimeoutSeconds);
    }

    private async Task ProcessAsync(SqlConnection c, SqlTransaction tx, int statementId, Guid handle)
    {
        StatementDocument doc = await StatementQueries.GetAsync(c, tx, statementId, db.CommandTimeoutSeconds)
            ?? throw new InvalidOperationException($"Statement {statementId} does not exist.");

        if (doc.Header.Status is "P" or "F")
        {
            log.LogWarning("Statement {StatementId} is already {Status}; request ignored.", statementId, StatementStatus.Name(doc.Header.Status));
            await EndConversationAsync(c, tx, handle);
            return;
        }

        string path = writer.Write(doc, DateTime.Now);
        string? docNo = await StatementQueries.CompleteAsync(c, tx, statementId, path, Actor, db.CommandTimeoutSeconds);
        await EndConversationAsync(c, tx, handle);
        log.LogInformation("Statement {StatementId} {Distributor} {Quarter} written to {Path}, document {DocumentNo}.",
            statementId, doc.Header.DistributorCode, doc.Header.Quarter, path, docNo ?? "-");
    }

    private async Task FailAsync(SqlConnection c, Guid handle, int statementId, string error)
    {
        // The request was rolled back to the queue; take it off again and end the conversation with the error.
        await using SqlTransaction tx = c.BeginTransaction();
        await c.ExecuteAsync(
            @"RECEIVE TOP (1) conversation_handle FROM rebate.StatementRequestQueue WHERE conversation_handle = @handle;",
            new { handle }, tx);
        if (statementId > 0)
            await StatementQueries.FailAsync(c, tx, statementId, error, Actor, db.CommandTimeoutSeconds);
        string description = error.Length > 3000 ? error[..3000] : error;
        await c.ExecuteAsync("END CONVERSATION @handle WITH ERROR = 50100 DESCRIPTION = @description;", new { handle, description }, tx);
        await tx.CommitAsync();
    }

    private static Task EndConversationAsync(SqlConnection c, SqlTransaction tx, Guid handle) =>
        c.ExecuteAsync("END CONVERSATION @handle;", new { handle }, tx);

    public static int ParseStatementId(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return 0;
        XElement root = XElement.Parse(body);
        return (int?)root.Attribute("StatementId") ?? 0;
    }
}
