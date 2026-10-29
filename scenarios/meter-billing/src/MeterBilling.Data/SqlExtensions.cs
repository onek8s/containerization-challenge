using System;
using System.Data;
using System.Data.SqlClient;

namespace Coho.Billing.Data
{
    internal static class SqlExtensions
    {
        public static SqlCommand Command(this SqlConnection connection, string sql, SqlTransaction transaction = null)
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = transaction;
            command.CommandTimeout = 120;
            return command;
        }

        public static SqlCommand With(this SqlCommand command, string name, object value)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
            return command;
        }

        public static SqlCommand WithDate(this SqlCommand command, string name, DateTime? value)
        {
            command.Parameters.Add(name, SqlDbType.Date).Value = value.HasValue ? (object)value.Value.Date : DBNull.Value;
            return command;
        }

        public static string GetStringOrNull(this IDataRecord record, string name)
        {
            var value = record[name];
            return value == DBNull.Value ? null : (string)value;
        }

        public static T? GetNullable<T>(this IDataRecord record, string name) where T : struct
        {
            var value = record[name];
            return value == DBNull.Value ? (T?)null : (T)Convert.ChangeType(value, typeof(T));
        }
    }
}
