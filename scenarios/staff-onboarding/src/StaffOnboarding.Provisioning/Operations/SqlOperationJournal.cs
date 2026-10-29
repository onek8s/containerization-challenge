using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using StaffOnboarding.Core.Provisioning;

namespace StaffOnboarding.Provisioning.Operations;

public sealed class SqlOperationJournal : IOperationJournal
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _connectionString;

    public SqlOperationJournal(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Onboarding")
                            ?? throw new InvalidOperationException("Connection string 'Onboarding' is missing.");
    }

    public ProvisioningResult? FindCompleted(string requestNumber, string step)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(
            "SELECT ResultJson FROM idm.Operation WHERE RequestNumber = @RequestNumber AND Step = @Step AND Succeeded = 1", connection);
        command.Parameters.Add("@RequestNumber", SqlDbType.VarChar, 20).Value = requestNumber;
        command.Parameters.Add("@Step", SqlDbType.VarChar, 40).Value = step;
        connection.Open();
        var json = command.ExecuteScalar() as string;
        return json == null ? null : JsonSerializer.Deserialize<ProvisioningResult>(json, Json);
    }

    public void Record(string requestNumber, string step, string? samAccountName, string caller, ProvisioningResult result)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand("idm.RecordOperation", connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@RequestNumber", SqlDbType.VarChar, 20).Value = requestNumber;
        command.Parameters.Add("@Step", SqlDbType.VarChar, 40).Value = step;
        command.Parameters.Add("@SamAccountName", SqlDbType.NVarChar, 20).Value = (object?)samAccountName ?? DBNull.Value;
        command.Parameters.Add("@Caller", SqlDbType.NVarChar, 100).Value = caller;
        command.Parameters.Add("@Succeeded", SqlDbType.Bit).Value = result.Succeeded;
        command.Parameters.Add("@DomainController", SqlDbType.NVarChar, 100).Value = (object?)result.DomainController ?? DBNull.Value;
        command.Parameters.Add("@ResultJson", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(result, Json);
        connection.Open();
        command.ExecuteNonQuery();
    }
}
