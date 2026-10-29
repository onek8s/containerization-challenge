using System;
using System.Data;
using System.Data.SqlClient;
using Wingtip.Invoices.Infrastructure.Configuration;

namespace Wingtip.Invoices.Infrastructure.Data
{
    public static class Db
    {
        public static SqlConnection Open()
        {
            var connection = new SqlConnection(HubSettings.ConnectionString);
            connection.Open();
            return connection;
        }

        public static SqlCommand Procedure(SqlConnection connection, string name, SqlTransaction transaction = null)
        {
            return new SqlCommand(name, connection, transaction) { CommandType = CommandType.StoredProcedure, CommandTimeout = 60 };
        }

        public static SqlParameter Add(this SqlCommand command, string name, SqlDbType type, object value, int size = 0)
        {
            var p = size > 0 ? new SqlParameter(name, type, size) : new SqlParameter(name, type);
            p.Value = value ?? DBNull.Value;
            command.Parameters.Add(p);
            return p;
        }

        public static SqlParameter AddOutput(this SqlCommand command, string name, SqlDbType type)
        {
            var p = new SqlParameter(name, type) { Direction = ParameterDirection.Output };
            command.Parameters.Add(p);
            return p;
        }

        public static string String(this IDataRecord r, string name)
        {
            var i = r.GetOrdinal(name);
            return r.IsDBNull(i) ? null : r.GetString(i);
        }

        public static T? Nullable<T>(this IDataRecord r, string name) where T : struct
        {
            var i = r.GetOrdinal(name);
            return r.IsDBNull(i) ? (T?)null : (T)r.GetValue(i);
        }
    }
}
