using System.Data;
using Microsoft.Data.SqlClient;
using StaffOnboarding.Core.Requests;

namespace StaffOnboarding.Portal.Data;

public sealed class OnboardingRepository
{
    private readonly string _connectionString;

    public OnboardingRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Onboarding")
                            ?? throw new InvalidOperationException("Connection string 'Onboarding' is missing.");
    }

    /// <summary>Reserves the next request number and stores the request before the workflow is started.</summary>
    public async Task<string> CreateRequestAsync(RequestKind kind, string subject, string? samAccountName, string? department,
        string? managerAccount, DateTime effectiveDate, string requestedBy, string payload)
    {
        await using var connection = await OpenAsync();
        await using var command = Procedure(connection, "onb.CreateRequest");
        command.Parameters.Add("@Kind", SqlDbType.TinyInt).Value = (byte)kind;
        command.Parameters.Add("@Subject", SqlDbType.NVarChar, 200).Value = subject;
        command.Parameters.Add("@SamAccountName", SqlDbType.NVarChar, 20).Value = (object?)samAccountName ?? DBNull.Value;
        command.Parameters.Add("@Department", SqlDbType.VarChar, 10).Value = (object?)department ?? DBNull.Value;
        command.Parameters.Add("@ManagerAccount", SqlDbType.NVarChar, 20).Value = (object?)managerAccount ?? DBNull.Value;
        command.Parameters.Add("@EffectiveDate", SqlDbType.Date).Value = effectiveDate.Date;
        command.Parameters.Add("@RequestedBy", SqlDbType.NVarChar, 100).Value = requestedBy;
        command.Parameters.Add("@Payload", SqlDbType.NVarChar, -1).Value = payload;
        var number = command.Parameters.Add("@RequestNumber", SqlDbType.VarChar, 20);
        number.Direction = ParameterDirection.Output;
        await command.ExecuteNonQueryAsync();
        return (string)number.Value;
    }

    public async Task MarkStartFailedAsync(string requestNumber, string error)
    {
        await using var connection = await OpenAsync();
        await using var command = Procedure(connection, "onb.MarkStartFailed");
        command.Parameters.Add("@RequestNumber", SqlDbType.VarChar, 20).Value = requestNumber;
        command.Parameters.Add("@Error", SqlDbType.NVarChar, -1).Value = error;
        await command.ExecuteNonQueryAsync();
    }

    public async Task AddTimelineAsync(string requestNumber, string actor, string eventName, string? detail)
    {
        await using var connection = await OpenAsync();
        await using var command = Procedure(connection, "onb.AddTimeline");
        command.Parameters.Add("@RequestNumber", SqlDbType.VarChar, 20).Value = requestNumber;
        command.Parameters.Add("@Actor", SqlDbType.NVarChar, 100).Value = actor;
        command.Parameters.Add("@Event", SqlDbType.NVarChar, 100).Value = eventName;
        command.Parameters.Add("@Detail", SqlDbType.NVarChar, -1).Value = (object?)detail ?? DBNull.Value;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<RequestRow>> ListRequestsAsync(RequestStatus? status, RequestKind? kind, string? requestedBy, int top = 200)
    {
        await using var connection = await OpenAsync();
        await using var command = new SqlCommand(@"
SELECT TOP (@Top) RequestNumber, Kind, Status, Subject, SamAccountName, Department, ManagerAccount, EffectiveDate, RequestedBy,
       SubmittedAtUtc, UpdatedAtUtc, NextStepAtUtc, NextStep, WorkflowInstanceId, Definition, Payload
FROM onb.Request
WHERE (@Status IS NULL OR Status = @Status) AND (@Kind IS NULL OR Kind = @Kind) AND (@RequestedBy IS NULL OR RequestedBy = @RequestedBy)
ORDER BY SubmittedAtUtc DESC", connection);
        command.Parameters.Add("@Top", SqlDbType.Int).Value = top;
        command.Parameters.Add("@Status", SqlDbType.TinyInt).Value = status.HasValue ? (byte)status.Value : DBNull.Value;
        command.Parameters.Add("@Kind", SqlDbType.TinyInt).Value = kind.HasValue ? (byte)kind.Value : DBNull.Value;
        command.Parameters.Add("@RequestedBy", SqlDbType.NVarChar, 100).Value = (object?)requestedBy ?? DBNull.Value;
        return await ReadAllAsync(command, ReadRequest);
    }

    public async Task<RequestRow?> GetRequestAsync(string requestNumber)
    {
        await using var connection = await OpenAsync();
        await using var command = new SqlCommand(@"
SELECT RequestNumber, Kind, Status, Subject, SamAccountName, Department, ManagerAccount, EffectiveDate, RequestedBy,
       SubmittedAtUtc, UpdatedAtUtc, NextStepAtUtc, NextStep, WorkflowInstanceId, Definition, Payload
FROM onb.Request WHERE RequestNumber = @RequestNumber", connection);
        command.Parameters.Add("@RequestNumber", SqlDbType.VarChar, 20).Value = requestNumber;
        return (await ReadAllAsync(command, ReadRequest)).FirstOrDefault();
    }

    public async Task<IReadOnlyList<TimelineRow>> GetTimelineAsync(string requestNumber)
    {
        await using var connection = await OpenAsync();
        await using var command = new SqlCommand(
            "SELECT AtUtc, Actor, Event, Detail FROM onb.Timeline WHERE RequestNumber = @RequestNumber ORDER BY Id", connection);
        command.Parameters.Add("@RequestNumber", SqlDbType.VarChar, 20).Value = requestNumber;
        return await ReadAllAsync(command, r => new TimelineRow(r.GetDateTime(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3)));
    }

    public async Task<AccountRow?> GetAccountAsync(string requestNumber)
    {
        await using var connection = await OpenAsync();
        await using var command = new SqlCommand(
            "SELECT SamAccountName, UserPrincipalName, Mail, DistinguishedName, HomeDirectory, CreatedAtUtc FROM onb.Account WHERE RequestNumber = @RequestNumber", connection);
        command.Parameters.Add("@RequestNumber", SqlDbType.VarChar, 20).Value = requestNumber;
        return (await ReadAllAsync(command, r => new AccountRow(r.GetString(0), Str(r, 1), Str(r, 2), Str(r, 3), Str(r, 4), r.GetDateTime(5)))).FirstOrDefault();
    }

    public async Task<IReadOnlyList<TaskRow>> GetTasksForRequestAsync(string requestNumber)
    {
        await using var connection = await OpenAsync();
        await using var command = new SqlCommand(TaskSelect + " WHERE t.RequestNumber = @RequestNumber ORDER BY t.CreatedAtUtc", connection);
        command.Parameters.Add("@RequestNumber", SqlDbType.VarChar, 20).Value = requestNumber;
        return await ReadAllAsync(command, ReadTask);
    }

    /// <summary>Open tasks assigned to the user or to one of the given "group:" assignees.</summary>
    public async Task<IReadOnlyList<TaskRow>> GetOpenTasksAsync(string account, IEnumerable<string> groupAssignees)
    {
        var assignees = new List<string> { account };
        assignees.AddRange(groupAssignees);

        await using var connection = await OpenAsync();
        await using var command = new SqlCommand(TaskSelect + " WHERE t.Status = 'Open' AND t.AssignedTo IN (SELECT value FROM STRING_SPLIT(@Assignees, ';')) ORDER BY t.DueAtUtc", connection);
        command.Parameters.Add("@Assignees", SqlDbType.NVarChar, 4000).Value = string.Join(';', assignees);
        return await ReadAllAsync(command, ReadTask);
    }

    public async Task<TaskRow?> GetTaskAsync(Guid taskId)
    {
        await using var connection = await OpenAsync();
        await using var command = new SqlCommand(TaskSelect + " WHERE t.TaskId = @TaskId", connection);
        command.Parameters.Add("@TaskId", SqlDbType.UniqueIdentifier).Value = taskId;
        return (await ReadAllAsync(command, ReadTask)).FirstOrDefault();
    }

    private const string TaskSelect = @"
SELECT t.TaskId, t.RequestNumber, t.Kind, t.AssignedTo, t.CreatedAtUtc, t.DueAtUtc, t.Status, t.Outcome, t.DecidedBy, t.DecidedAtUtc,
       t.Comment, t.Reminders, r.Subject, r.Kind
FROM onb.ApprovalTask t JOIN onb.Request r ON r.RequestNumber = t.RequestNumber";

    private static TaskRow ReadTask(SqlDataReader r) => new(
        r.GetGuid(0), r.GetString(1), (TaskKind)r.GetByte(2), r.GetString(3), r.GetDateTime(4), r.GetDateTime(5), r.GetString(6),
        r.IsDBNull(7) ? null : (DecisionOutcome)r.GetByte(7), Str(r, 8), r.IsDBNull(9) ? null : r.GetDateTime(9), Str(r, 10),
        r.GetInt32(11), r.GetString(12), (RequestKind)r.GetByte(13));

    private static RequestRow ReadRequest(SqlDataReader r) => new(
        r.GetString(0), (RequestKind)r.GetByte(1), (RequestStatus)r.GetByte(2), r.GetString(3), Str(r, 4), Str(r, 5), Str(r, 6),
        r.GetDateTime(7), r.GetString(8), r.GetDateTime(9), r.GetDateTime(10), r.IsDBNull(11) ? null : r.GetDateTime(11), Str(r, 12),
        r.IsDBNull(13) ? null : r.GetGuid(13), Str(r, 14), r.GetString(15));

    private static string? Str(SqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);

    private async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static SqlCommand Procedure(SqlConnection connection, string name) =>
        new(name, connection) { CommandType = CommandType.StoredProcedure };

    private static async Task<IReadOnlyList<T>> ReadAllAsync<T>(SqlCommand command, Func<SqlDataReader, T> map)
    {
        var list = new List<T>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) list.Add(map(reader));
        return list;
    }
}
