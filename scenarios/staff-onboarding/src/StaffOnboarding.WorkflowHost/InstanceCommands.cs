using System;
using System.Activities;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.ServiceModel;
using System.ServiceModel.Activities;

namespace StaffOnboarding.WorkflowHost
{
    /// <summary>Operator commands against persisted workflow instances.</summary>
    internal static class InstanceCommands
    {
        private const string ListSql = @"
SELECT p.Value1 AS RequestNumber, p.Value2 AS Kind, p.Value3 AS Status, p.Value4 AS Subject, p.Value5 AS NextStepAtUtc,
       i.InstanceId, i.IdentityName, i.Major, i.Minor, i.IsSuspended, i.SuspensionReason, i.ExecutionStatus,
       i.PendingTimer, i.CurrentMachine, i.LastMachine, i.LastUpdatedTime
FROM [System.Activities.DurableInstancing].[Instances] i
JOIN [System.Activities.DurableInstancing].[InstancePromotedProperties] p
  ON p.InstanceId = i.InstanceId AND p.PromotionName = @PromotionName
WHERE i.IsCompleted = 0 AND (@Status IS NULL OR CAST(p.Value3 AS nvarchar(50)) = @Status)
ORDER BY i.PendingTimer, i.LastUpdatedTime";

        public static int List(string status)
        {
            var cs = ConfigurationManager.ConnectionStrings["InstanceStore"].ConnectionString;
            using (var connection = new SqlConnection(cs))
            using (var command = new SqlCommand(ListSql, connection))
            {
                command.Parameters.Add("@PromotionName", SqlDbType.NVarChar, 400).Value = Workflows.Services.RequestPromotion.PromotionName;
                command.Parameters.Add("@Status", SqlDbType.NVarChar, 50).Value = (object)status ?? DBNull.Value;
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    Console.WriteLine("{0,-15} {1,-7} {2,-20} {3,-36} {4,-12} {5,-20} {6}", "Request", "Kind", "Status", "Instance", "Version", "Next timer (UTC)", "Machine");
                    while (reader.Read())
                    {
                        var suspended = !reader.IsDBNull(9) && reader.GetBoolean(9);
                        Console.WriteLine("{0,-15} {1,-7} {2,-20} {3,-36} {4,-12} {5,-20} {6}{7}",
                            reader["RequestNumber"], reader["Kind"], reader["Status"], reader["InstanceId"],
                            reader["IdentityName"] + " " + reader["Major"] + "." + reader["Minor"],
                            reader.IsDBNull(12) ? "" : reader.GetDateTime(12).ToString("yyyy-MM-dd HH:mm"),
                            reader.IsDBNull(13) ? reader["LastMachine"] : reader["CurrentMachine"],
                            suspended ? "  SUSPENDED: " + reader["SuspensionReason"] : "");
                    }
                }
            }
            return 0;
        }

        public static int Control(string action, string service, Guid instanceId, string reason)
        {
            var binding = new NetNamedPipeBinding(NetNamedPipeSecurityMode.Transport);
            var client = new WorkflowControlClient(binding, new EndpointAddress(Bindings.ControlAddress(service.ToLowerInvariant())));
            try
            {
                switch (action)
                {
                    case "suspend": client.Suspend(instanceId, reason); break;
                    case "unsuspend": client.Unsuspend(instanceId); break;
                    case "terminate": client.Terminate(instanceId, reason); break;
                    case "cancel": client.Cancel(instanceId); break;
                }
                client.Close();
            }
            catch (FaultException ex)
            {
                client.Abort();
                Console.Error.WriteLine(action + " failed: " + ex.Message);
                return 1;
            }
            Console.WriteLine(action + " sent to " + instanceId);
            return 0;
        }
    }
}
