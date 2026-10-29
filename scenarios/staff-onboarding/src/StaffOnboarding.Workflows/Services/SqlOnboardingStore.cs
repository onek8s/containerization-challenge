using System;
using System.Data;
using System.Data.SqlClient;
using StaffOnboarding.Core.Requests;

namespace StaffOnboarding.Workflows.Services
{
    public class SqlOnboardingStore : IOnboardingStore
    {
        private readonly string _connectionString;

        public SqlOnboardingStore(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void AttachInstance(string requestNumber, Guid instanceId, string definition)
        {
            Execute("onb.AttachInstance", p =>
            {
                p.Add("@RequestNumber", SqlDbType.VarChar, 20).Value = requestNumber;
                p.Add("@InstanceId", SqlDbType.UniqueIdentifier).Value = instanceId;
                p.Add("@Definition", SqlDbType.VarChar, 64).Value = definition;
            });
        }

        public void SetStatus(string requestNumber, RequestStatus status)
        {
            Execute("onb.SetStatus", p =>
            {
                p.Add("@RequestNumber", SqlDbType.VarChar, 20).Value = requestNumber;
                p.Add("@Status", SqlDbType.TinyInt).Value = (byte)status;
            });
        }

        public void SetSchedule(string requestNumber, DateTime? nextStepAtUtc, string nextStep)
        {
            Execute("onb.SetSchedule", p =>
            {
                p.Add("@RequestNumber", SqlDbType.VarChar, 20).Value = requestNumber;
                p.Add("@NextStepAtUtc", SqlDbType.DateTime2).Value = (object)nextStepAtUtc ?? DBNull.Value;
                p.Add("@NextStep", SqlDbType.NVarChar, 100).Value = (object)nextStep ?? DBNull.Value;
            });
        }

        public void AddTimeline(string requestNumber, string actor, string eventName, string detail)
        {
            Execute("onb.AddTimeline", p =>
            {
                p.Add("@RequestNumber", SqlDbType.VarChar, 20).Value = requestNumber;
                p.Add("@Actor", SqlDbType.NVarChar, 100).Value = (object)actor ?? "system";
                p.Add("@Event", SqlDbType.NVarChar, 100).Value = eventName;
                p.Add("@Detail", SqlDbType.NVarChar, -1).Value = (object)detail ?? DBNull.Value;
            });
        }

        public void CreateTask(Guid taskId, string requestNumber, TaskKind kind, string assignedTo, DateTime dueUtc)
        {
            Execute("onb.CreateTask", p =>
            {
                p.Add("@TaskId", SqlDbType.UniqueIdentifier).Value = taskId;
                p.Add("@RequestNumber", SqlDbType.VarChar, 20).Value = requestNumber;
                p.Add("@Kind", SqlDbType.TinyInt).Value = (byte)kind;
                p.Add("@AssignedTo", SqlDbType.NVarChar, 100).Value = assignedTo;
                p.Add("@DueAtUtc", SqlDbType.DateTime2).Value = dueUtc;
            });
        }

        public void CloseTask(Guid taskId, string status, DecisionOutcome? outcome, string decidedBy, string comment)
        {
            Execute("onb.CloseTask", p =>
            {
                p.Add("@TaskId", SqlDbType.UniqueIdentifier).Value = taskId;
                p.Add("@Status", SqlDbType.VarChar, 20).Value = status;
                p.Add("@Outcome", SqlDbType.TinyInt).Value = outcome.HasValue ? (object)(byte)outcome.Value : DBNull.Value;
                p.Add("@DecidedBy", SqlDbType.NVarChar, 100).Value = (object)decidedBy ?? DBNull.Value;
                p.Add("@Comment", SqlDbType.NVarChar, 2000).Value = (object)comment ?? DBNull.Value;
            });
        }

        public void CountReminder(Guid taskId)
        {
            Execute("onb.CountReminder", p => p.Add("@TaskId", SqlDbType.UniqueIdentifier).Value = taskId);
        }

        public void CloseOpenTasks(string requestNumber, string status)
        {
            Execute("onb.CloseOpenTasks", p =>
            {
                p.Add("@RequestNumber", SqlDbType.VarChar, 20).Value = requestNumber;
                p.Add("@Status", SqlDbType.VarChar, 20).Value = status;
            });
        }

        public void SaveAccount(string requestNumber, string samAccountName, string userPrincipalName, string mail, string distinguishedName, string homeDirectory)
        {
            Execute("onb.SaveAccount", p =>
            {
                p.Add("@RequestNumber", SqlDbType.VarChar, 20).Value = requestNumber;
                p.Add("@SamAccountName", SqlDbType.NVarChar, 20).Value = samAccountName;
                p.Add("@UserPrincipalName", SqlDbType.NVarChar, 256).Value = (object)userPrincipalName ?? DBNull.Value;
                p.Add("@Mail", SqlDbType.NVarChar, 256).Value = (object)mail ?? DBNull.Value;
                p.Add("@DistinguishedName", SqlDbType.NVarChar, 400).Value = (object)distinguishedName ?? DBNull.Value;
                p.Add("@HomeDirectory", SqlDbType.NVarChar, 260).Value = (object)homeDirectory ?? DBNull.Value;
            });
        }

        public void SetSubjectAccount(string requestNumber, string samAccountName)
        {
            Execute("onb.SetSubjectAccount", p =>
            {
                p.Add("@RequestNumber", SqlDbType.VarChar, 20).Value = requestNumber;
                p.Add("@SamAccountName", SqlDbType.NVarChar, 20).Value = samAccountName;
            });
        }

        private void Execute(string procedure, Action<SqlParameterCollection> parameters)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(procedure, connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 30;
                parameters(command.Parameters);
                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }
}
