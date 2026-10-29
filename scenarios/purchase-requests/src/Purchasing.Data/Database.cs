using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace Contoso.Purchasing.Data
{
    public class Database
    {
        public const string DefaultConnectionName = "Purchasing";

        private readonly string _connectionString;

        public Database() : this(DefaultConnectionName)
        {
        }

        public Database(string connectionName)
        {
            var settings = ConfigurationManager.ConnectionStrings[connectionName];
            if (settings == null)
            {
                throw new ConfigurationErrorsException("Connection string '" + connectionName + "' is missing.");
            }
            _connectionString = settings.ConnectionString;
        }

        public SqlConnection Open()
        {
            var connection = new SqlConnection(_connectionString);
            connection.Open();
            return connection;
        }

        internal static SqlCommand Command(SqlConnection connection, string sql, SqlTransaction transaction = null)
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 60;
            command.Transaction = transaction;
            return command;
        }

        internal static object DbValue(object value)
        {
            return value ?? DBNull.Value;
        }

        internal static string GetString(IDataRecord record, string name)
        {
            var ordinal = record.GetOrdinal(name);
            return record.IsDBNull(ordinal) ? null : record.GetString(ordinal);
        }

        internal static DateTime? GetDate(IDataRecord record, string name)
        {
            var ordinal = record.GetOrdinal(name);
            return record.IsDBNull(ordinal) ? (DateTime?)null : record.GetDateTime(ordinal);
        }
    }
}
