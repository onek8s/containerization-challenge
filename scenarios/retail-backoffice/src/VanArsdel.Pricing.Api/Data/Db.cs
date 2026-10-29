using System;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Reflection;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace VanArsdel.Pricing.Api.Data
{
    public interface IDbConnectionFactory
    {
        DbConnection Open();
        bool IsSqlite { get; }

        /// <summary>"INSERT ... ON CONFLICT" for SQLite, "MERGE" for SQL Server.</summary>
        string Upsert(string table, string[] keyColumns, string[] valueColumns);
    }

    public class DbConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public DbConnectionFactory(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("Pricing");
            IsSqlite = string.Equals(configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);
        }

        public bool IsSqlite { get; }

        public DbConnection Open()
        {
            DbConnection connection = IsSqlite ? (DbConnection)new SqliteConnection(_connectionString) : new SqlConnection(_connectionString);
            connection.Open();
            if (IsSqlite) connection.Execute("PRAGMA foreign_keys = ON;");
            return connection;
        }

        public string Upsert(string table, string[] keyColumns, string[] valueColumns)
        {
            var all = new string[keyColumns.Length + valueColumns.Length];
            keyColumns.CopyTo(all, 0);
            valueColumns.CopyTo(all, keyColumns.Length);
            if (IsSqlite)
            {
                return $"INSERT INTO {table} ({string.Join(", ", all)}) VALUES ({string.Join(", ", Array.ConvertAll(all, c => "@" + c))}) " +
                       $"ON CONFLICT ({string.Join(", ", keyColumns)}) DO UPDATE SET {string.Join(", ", Array.ConvertAll(valueColumns, c => c + " = excluded." + c))};";
            }
            return $"MERGE dbo.{table} WITH (HOLDLOCK) AS t USING (SELECT {string.Join(", ", Array.ConvertAll(all, c => "@" + c + " AS " + c))}) AS s " +
                   $"ON {string.Join(" AND ", Array.ConvertAll(keyColumns, c => "t." + c + " = s." + c))} " +
                   $"WHEN MATCHED THEN UPDATE SET {string.Join(", ", Array.ConvertAll(valueColumns, c => "t." + c + " = s." + c))} " +
                   $"WHEN NOT MATCHED THEN INSERT ({string.Join(", ", all)}) VALUES ({string.Join(", ", Array.ConvertAll(all, c => "s." + c))});";
        }

        /// <summary>Creates the SQLite schema on developer machines.</summary>
        public void CreateSqliteSchema()
        {
            var path = new SqliteConnectionStringBuilder(_connectionString).DataSource;
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("VanArsdel.Pricing.Api.Sql.schema.sqlite.sql");
            using var reader = new StreamReader(stream);
            using var connection = Open();
            connection.Execute(reader.ReadToEnd());
        }
    }

    /// <summary>Dapper handlers so DATE/DATETIME2 round-trip as text in SQLite.</summary>
    public static class DapperConfig
    {
        public static void Register()
        {
            SqlMapper.AddTypeHandler(new DateTimeHandler());
            SqlMapper.RemoveTypeMap(typeof(DateTime));
            SqlMapper.RemoveTypeMap(typeof(DateTime?));
        }

        private class DateTimeHandler : SqlMapper.TypeHandler<DateTime>
        {
            public override void SetValue(IDbDataParameter parameter, DateTime value)
            {
                parameter.Value = value;
                if (parameter is SqliteParameter) parameter.Value = value.ToString("yyyy-MM-dd HH:mm:ss.fffffff");
            }

            public override DateTime Parse(object value) => value is DateTime d ? d : DateTime.Parse(value.ToString(), System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
