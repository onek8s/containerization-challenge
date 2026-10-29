using System;
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

namespace Fabrikam.Housing.Data
{
    /// <summary>Plain ADO.NET connections for Dapper queries.</summary>
    public class ConnectionFactory
    {
        public ConnectionFactory(string provider, string connectionString)
        {
            Provider = provider;
            ConnectionString = connectionString;
        }

        public string Provider { get; }
        public string ConnectionString { get; }
        public bool IsSqlite => string.Equals(Provider, "Sqlite", StringComparison.OrdinalIgnoreCase);

        /// <summary>Table name with schema for raw SQL.</summary>
        public string Table(string name) => IsSqlite ? name : "housing." + name;

        public DbConnection Open()
        {
            DbConnection connection = IsSqlite ? (DbConnection)new SqliteConnection(ConnectionString) : new SqlConnection(ConnectionString);
            connection.Open();
            return connection;
        }
    }
}
