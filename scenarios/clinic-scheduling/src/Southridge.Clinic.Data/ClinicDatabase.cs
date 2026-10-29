using System;
using System.Data;
using System.Data.Common;
using Microsoft.Practices.EnterpriseLibrary.Data;

namespace Southridge.Clinic.Data
{
    /// <summary>
    /// Access to the Enterprise Library Data Access Block database "Clinic" and small helpers for reading
    /// stored procedure results.
    /// </summary>
    public abstract class RepositoryBase
    {
        public const string DatabaseName = "Clinic";

        private Database _database;

        protected Database Db
        {
            get
            {
                if (_database == null)
                {
                    _database = DatabaseFactory.CreateDatabase(DatabaseName);
                }

                return _database;
            }
        }

        protected DbCommand Proc(string name)
        {
            DbCommand command = Db.GetStoredProcCommand(name);
            command.CommandTimeout = 120;
            return command;
        }

        protected void In(DbCommand command, string name, DbType type, object value)
        {
            Db.AddInParameter(command, name, type, value ?? DBNull.Value);
        }

        protected void Out(DbCommand command, string name, DbType type, int size)
        {
            Db.AddOutParameter(command, name, type, size);
        }

        protected T OutValue<T>(DbCommand command, string name)
        {
            object value = Db.GetParameterValue(command, name);
            return value == null || value == DBNull.Value ? default(T) : (T)Convert.ChangeType(value, typeof(T));
        }

        protected static string GetString(IDataRecord r, string column)
        {
            object v = r[column];
            return v == DBNull.Value ? null : Convert.ToString(v).TrimEnd();
        }

        protected static int GetInt(IDataRecord r, string column)
        {
            return Convert.ToInt32(r[column]);
        }

        protected static int? GetNullableInt(IDataRecord r, string column)
        {
            object v = r[column];
            return v == DBNull.Value ? (int?)null : Convert.ToInt32(v);
        }

        protected static decimal GetDecimal(IDataRecord r, string column)
        {
            object v = r[column];
            return v == DBNull.Value ? 0m : Convert.ToDecimal(v);
        }

        protected static DateTime GetDate(IDataRecord r, string column)
        {
            return Convert.ToDateTime(r[column]);
        }

        protected static DateTime? GetNullableDate(IDataRecord r, string column)
        {
            object v = r[column];
            return v == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(v);
        }

        protected static bool GetBool(IDataRecord r, string column)
        {
            object v = r[column];
            return v != DBNull.Value && Convert.ToBoolean(v);
        }
    }

    /// <summary>Creates LINQ to SQL contexts. Load options are set per use by the caller.</summary>
    public static class ClinicDataContextFactory
    {
        public static ClinicDataContext Create()
        {
            var context = new ClinicDataContext();
            context.CommandTimeout = 60;
            return context;
        }

        public static ClinicDataContext CreateReadOnly()
        {
            ClinicDataContext context = Create();
            context.ObjectTrackingEnabled = false;
            return context;
        }
    }
}
