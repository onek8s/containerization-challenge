using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Northwind.Inspections.Infrastructure.Data
{
    public sealed class Db
    {
        private readonly string connectionString;

        public Db(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string is empty.", "connectionString");
            }
            this.connectionString = connectionString;
        }

        public SqlConnection Open()
        {
            var connection = new SqlConnection(connectionString);
            connection.Open();
            return connection;
        }

        public List<T> Query<T>(string sql, Func<IDataRecord, T> map, params SqlParameter[] parameters)
        {
            using (var connection = Open())
            using (var command = Command(connection, null, sql, parameters))
            using (var reader = command.ExecuteReader())
            {
                var list = new List<T>();
                while (reader.Read())
                {
                    list.Add(map(reader));
                }
                return list;
            }
        }

        public T Single<T>(string sql, Func<IDataRecord, T> map, params SqlParameter[] parameters) where T : class
        {
            var list = Query(sql, map, parameters);
            return list.Count == 0 ? null : list[0];
        }

        public int Execute(string sql, params SqlParameter[] parameters)
        {
            using (var connection = Open())
            using (var command = Command(connection, null, sql, parameters))
            {
                return command.ExecuteNonQuery();
            }
        }

        public object Scalar(string sql, params SqlParameter[] parameters)
        {
            using (var connection = Open())
            using (var command = Command(connection, null, sql, parameters))
            {
                var value = command.ExecuteScalar();
                return value == DBNull.Value ? null : value;
            }
        }

        public static SqlCommand Command(SqlConnection connection, SqlTransaction transaction, string sql, params SqlParameter[] parameters)
        {
            var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = 60 };
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }
            return command;
        }

        public static SqlParameter P(string name, object value)
        {
            return new SqlParameter(name, value ?? DBNull.Value);
        }

        public static SqlParameter P(string name, SqlDbType type, int size, object value)
        {
            return new SqlParameter(name, type, size) { Value = value ?? DBNull.Value };
        }

        public static string Str(IDataRecord r, string name)
        {
            var o = r[name];
            return o == DBNull.Value ? null : (string)o;
        }

        public static DateTime? Date(IDataRecord r, string name)
        {
            var o = r[name];
            return o == DBNull.Value ? (DateTime?)null : (DateTime)o;
        }

        public static decimal? Dec(IDataRecord r, string name)
        {
            var o = r[name];
            return o == DBNull.Value ? (decimal?)null : (decimal)o;
        }
    }
}
