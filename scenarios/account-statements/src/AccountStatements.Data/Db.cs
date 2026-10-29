using System;
using System.Configuration;
using System.Data.SqlClient;

namespace Woodgrove.Statements.Data
{
    public class Db
    {
        public const string ConnectionStringName = "AccountStatements";
        readonly string _connectionString;

        public Db(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("Connection string is empty.", nameof(connectionString));
            _connectionString = connectionString;
        }

        public static Db FromConfig()
        {
            var cs = ConfigurationManager.ConnectionStrings[ConnectionStringName];
            if (cs == null) throw new ConfigurationErrorsException("Connection string '" + ConnectionStringName + "' is missing.");
            return new Db(cs.ConnectionString);
        }

        public SqlConnection Open()
        {
            var connection = new SqlConnection(_connectionString);
            connection.Open();
            return connection;
        }
    }
}
