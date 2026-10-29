using System;
using System.Data;
using System.Data.Common;
using Microsoft.Practices.EnterpriseLibrary.Data;

namespace VanArsdel.Integration.Data
{
    /// <summary>
    /// Entry point to the VanArsdelIntegration database through the Enterprise Library Data Access Application Block.
    /// The connection string name comes from the dataConfiguration section of the host's config file
    /// (BTSNTSvc64.exe.config on the BizTalk servers, web.config in the portals).
    /// </summary>
    public class IntegrationDatabase
    {
        public const string DefaultName = "VanArsdelIntegration";
        private static readonly object FactoryGate = new object();
        private static bool _factorySet;

        public IntegrationDatabase(string name = DefaultName)
        {
            EnsureFactory();
            Database = new DatabaseProviderFactory().Create(name);
        }

        public IntegrationDatabase(Database database)
        {
            Database = database;
        }

        public Database Database { get; private set; }

        public static void EnsureFactory()
        {
            lock (FactoryGate)
            {
                if (_factorySet)
                {
                    return;
                }

                try
                {
                    DatabaseFactory.SetDatabaseProviderFactory(new DatabaseProviderFactory(), false);
                }
                catch (InvalidOperationException)
                {
                    // already set by the host
                }

                _factorySet = true;
            }
        }

        public DbCommand Procedure(string name, int timeoutSeconds = 30)
        {
            var cmd = Database.GetStoredProcCommand(name);
            cmd.CommandTimeout = timeoutSeconds;
            return cmd;
        }

        public DbCommand Text(string sql, int timeoutSeconds = 30)
        {
            var cmd = Database.GetSqlStringCommand(sql);
            cmd.CommandTimeout = timeoutSeconds;
            return cmd;
        }

        public void In(DbCommand cmd, string name, DbType type, object value)
        {
            Database.AddInParameter(cmd, name, type, value ?? DBNull.Value);
        }

        public static string S(IDataRecord r, string column)
        {
            var i = r.GetOrdinal(column);
            return r.IsDBNull(i) ? null : r.GetString(i);
        }

        public static int I(IDataRecord r, string column)
        {
            var i = r.GetOrdinal(column);
            return r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i));
        }

        public static decimal M(IDataRecord r, string column)
        {
            var i = r.GetOrdinal(column);
            return r.IsDBNull(i) ? 0m : Convert.ToDecimal(r.GetValue(i));
        }

        public static bool B(IDataRecord r, string column)
        {
            var i = r.GetOrdinal(column);
            return !r.IsDBNull(i) && Convert.ToBoolean(r.GetValue(i));
        }

        public static DateTime? D(IDataRecord r, string column)
        {
            var i = r.GetOrdinal(column);
            return r.IsDBNull(i) ? (DateTime?)null : DateTime.SpecifyKind(r.GetDateTime(i), DateTimeKind.Utc);
        }
    }
}
