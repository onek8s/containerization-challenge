using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using CreditDesk.Core;

namespace CreditDesk.CreditService
{
    /// <summary>
    /// Data access through the cd.* stored procedures. Connections use integrated security and are
    /// opened while impersonating the caller, so SQL Server sees the signed-in user (SUSER_SNAME()).
    /// </summary>
    internal sealed class CreditRepository
    {
        private readonly string _connectionString;

        public CreditRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IList<CustomerAccount> SearchCustomers(string text, int max)
        {
            using (var cmd = Procedure("cd.SearchCustomers"))
            {
                cmd.Parameters.Add("@Text", SqlDbType.NVarChar, 100).Value = text ?? "";
                cmd.Parameters.Add("@Max", SqlDbType.Int).Value = max;
                return ReadList(cmd, ReadCustomer);
            }
        }

        public CustomerAccount GetCustomer(string accountNumber)
        {
            using (var cmd = Procedure("cd.GetCustomer"))
            {
                cmd.Parameters.Add("@AccountNumber", SqlDbType.VarChar, 20).Value = accountNumber;
                var list = ReadList(cmd, ReadCustomer);
                return list.Count == 0 ? null : list[0];
            }
        }

        public CreditRequest Insert(CustomerAccount account, decimal requestedLimit, string reason, ApprovalLevel level)
        {
            using (var cmd = Procedure("cd.InsertRequest"))
            {
                cmd.Parameters.Add("@AccountNumber", SqlDbType.VarChar, 20).Value = account.AccountNumber;
                cmd.Parameters.Add("@CurrentLimit", SqlDbType.Decimal).Value = account.CreditLimit;
                cmd.Parameters.Add("@RequestedLimit", SqlDbType.Decimal).Value = requestedLimit;
                cmd.Parameters.Add("@Reason", SqlDbType.NVarChar, 1000).Value = reason.Trim();
                cmd.Parameters.Add("@RequiredLevel", SqlDbType.TinyInt).Value = (byte)level;
                return ReadList(cmd, ReadRequest)[0];
            }
        }

        public CreditRequest GetRequest(int requestId)
        {
            using (var cmd = Procedure("cd.GetRequest"))
            {
                cmd.Parameters.Add("@RequestId", SqlDbType.Int).Value = requestId;
                var list = ReadList(cmd, ReadRequest);
                return list.Count == 0 ? null : list[0];
            }
        }

        public IList<CreditRequest> GetRequestsByRequester()
        {
            using (var cmd = Procedure("cd.GetMyRequests"))
                return ReadList(cmd, ReadRequest);
        }

        public IList<CreditRequest> GetPending(ApprovalLevel maxLevel)
        {
            using (var cmd = Procedure("cd.GetPendingRequests"))
            {
                cmd.Parameters.Add("@MaxLevel", SqlDbType.TinyInt).Value = (byte)maxLevel;
                return ReadList(cmd, ReadRequest);
            }
        }

        public IList<CreditRequest> GetForCustomer(string accountNumber)
        {
            using (var cmd = Procedure("cd.GetRequestsForCustomer"))
            {
                cmd.Parameters.Add("@AccountNumber", SqlDbType.VarChar, 20).Value = accountNumber;
                return ReadList(cmd, ReadRequest);
            }
        }

        /// <summary>Records the decision and, when approved, updates the customer's limit and history.</summary>
        public CreditRequest Decide(int requestId, RequestStatus status, string comment, DateTime? validUntil)
        {
            using (var cmd = Procedure("cd.DecideRequest"))
            {
                cmd.Parameters.Add("@RequestId", SqlDbType.Int).Value = requestId;
                cmd.Parameters.Add("@Status", SqlDbType.TinyInt).Value = (byte)status;
                cmd.Parameters.Add("@Comment", SqlDbType.NVarChar, 1000).Value = (object)comment ?? DBNull.Value;
                cmd.Parameters.Add("@ValidUntil", SqlDbType.Date).Value = (object)validUntil ?? DBNull.Value;
                return ReadList(cmd, ReadRequest)[0];
            }
        }

        private SqlCommand Procedure(string name)
        {
            var connection = new SqlConnection(_connectionString);
            var cmd = new SqlCommand(name, connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = 30 };
            cmd.Disposed += (s, e) => connection.Dispose();
            return cmd;
        }

        private static List<T> ReadList<T>(SqlCommand cmd, Func<SqlDataReader, T> read)
        {
            var list = new List<T>();
            cmd.Connection.Open();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                    list.Add(read(reader));
            }
            return list;
        }

        private static CustomerAccount ReadCustomer(SqlDataReader r) => new CustomerAccount
        {
            AccountNumber = r.GetString(r.GetOrdinal("AccountNumber")),
            Name = r.GetString(r.GetOrdinal("Name")),
            ContactName = String(r, "ContactName"),
            AddressLine1 = String(r, "AddressLine1"),
            AddressLine2 = String(r, "AddressLine2"),
            PostalCode = String(r, "PostalCode"),
            City = String(r, "City"),
            CountryCode = String(r, "CountryCode"),
            SalesRep = String(r, "SalesRep"),
            CreditLimit = r.GetDecimal(r.GetOrdinal("CreditLimit")),
            OpenBalance = r.GetDecimal(r.GetOrdinal("OpenBalance")),
            OverdueBalance = r.GetDecimal(r.GetOrdinal("OverdueBalance")),
            OldestOverdueDays = r.GetInt32(r.GetOrdinal("OldestOverdueDays")),
            CustomerSince = Date(r, "CustomerSince"),
            OnStop = r.GetBoolean(r.GetOrdinal("OnStop"))
        };

        private static CreditRequest ReadRequest(SqlDataReader r) => new CreditRequest
        {
            RequestId = r.GetInt32(r.GetOrdinal("RequestId")),
            AccountNumber = r.GetString(r.GetOrdinal("AccountNumber")),
            CustomerName = r.GetString(r.GetOrdinal("CustomerName")),
            CurrentLimit = r.GetDecimal(r.GetOrdinal("CurrentLimit")),
            RequestedLimit = r.GetDecimal(r.GetOrdinal("RequestedLimit")),
            Reason = r.GetString(r.GetOrdinal("Reason")),
            Status = (RequestStatus)r.GetByte(r.GetOrdinal("Status")),
            RequiredLevel = (ApprovalLevel)r.GetByte(r.GetOrdinal("RequiredLevel")),
            RequestedBy = r.GetString(r.GetOrdinal("RequestedBy")),
            RequestedAt = r.GetDateTime(r.GetOrdinal("RequestedAt")),
            DecidedBy = String(r, "DecidedBy"),
            DecidedAt = Date(r, "DecidedAt"),
            DecisionComment = String(r, "DecisionComment"),
            ValidUntil = Date(r, "ValidUntil"),
            LetterPath = String(r, "LetterPath")
        };

        private static string String(SqlDataReader r, string column)
        {
            var i = r.GetOrdinal(column);
            return r.IsDBNull(i) ? null : r.GetString(i);
        }

        private static DateTime? Date(SqlDataReader r, string column)
        {
            var i = r.GetOrdinal(column);
            return r.IsDBNull(i) ? (DateTime?)null : r.GetDateTime(i);
        }
    }
}
