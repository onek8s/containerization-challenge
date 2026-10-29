using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace Tailspin.CustomerCare.Data
{
    /// <summary>Plain ADO.NET access for the stored procedures and bulk operations.</summary>
    public static class Db
    {
        public const int DefaultTimeoutSeconds = 60;

        public static string ConnectionString
        {
            get
            {
                var cs = ConfigurationManager.ConnectionStrings["CustomerCare"];
                if (cs == null) throw new ConfigurationErrorsException("Connection string 'CustomerCare' is missing.");
                return cs.ConnectionString;
            }
        }

        public static SqlConnection Open()
        {
            var cn = new SqlConnection(ConnectionString);
            cn.Open();
            return cn;
        }

        public static SqlCommand Proc(SqlConnection cn, string name, params SqlParameter[] parameters)
        {
            var cmd = new SqlCommand(name, cn) { CommandType = CommandType.StoredProcedure, CommandTimeout = DefaultTimeoutSeconds };
            if (parameters != null) cmd.Parameters.AddRange(parameters);
            return cmd;
        }

        public static SqlParameter P(string name, object value)
        {
            return new SqlParameter(name, value ?? DBNull.Value);
        }

        public static SqlParameter P(string name, SqlDbType type, object value)
        {
            return new SqlParameter(name, type) { Value = value ?? DBNull.Value };
        }

        public static List<T> Query<T>(string proc, Func<SqlDataReader, T> map, params SqlParameter[] parameters)
        {
            var list = new List<T>();
            using (var cn = Open())
            using (var cmd = Proc(cn, proc, parameters))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read()) list.Add(map(r));
            }

            return list;
        }

        public static int Execute(string proc, params SqlParameter[] parameters)
        {
            using (var cn = Open())
            using (var cmd = Proc(cn, proc, parameters))
            {
                return cmd.ExecuteNonQuery();
            }
        }

        public static T Scalar<T>(string proc, params SqlParameter[] parameters)
        {
            using (var cn = Open())
            using (var cmd = Proc(cn, proc, parameters))
            {
                var v = cmd.ExecuteScalar();
                return v == null || v == DBNull.Value ? default(T) : (T)Convert.ChangeType(v, typeof(T));
            }
        }

        public static string GetString(this SqlDataReader r, string column)
        {
            var i = r.GetOrdinal(column);
            return r.IsDBNull(i) ? null : r.GetString(i);
        }

        public static int GetInt(this SqlDataReader r, string column)
        {
            return r.GetInt32(r.GetOrdinal(column));
        }

        public static DateTime GetDate(this SqlDataReader r, string column)
        {
            return DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal(column)), DateTimeKind.Utc);
        }

        public static DateTime? GetNullableDate(this SqlDataReader r, string column)
        {
            var i = r.GetOrdinal(column);
            return r.IsDBNull(i) ? (DateTime?)null : DateTime.SpecifyKind(r.GetDateTime(i), DateTimeKind.Utc);
        }

        public static double? GetNullableDouble(this SqlDataReader r, string column)
        {
            var i = r.GetOrdinal(column);
            return r.IsDBNull(i) ? (double?)null : Convert.ToDouble(r.GetValue(i));
        }
    }
}
