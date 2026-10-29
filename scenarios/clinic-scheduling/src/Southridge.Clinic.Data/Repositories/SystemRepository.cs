using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Web;
using System.Web.Caching;

namespace Southridge.Clinic.Data.Repositories
{
    /// <summary>Settings table, audit log, job runs and API clients.</summary>
    public class SystemRepository : RepositoryBase
    {
        private const string SettingsCacheKey = "Southridge.AppSettings";
        private static readonly object SettingsLock = new object();
        private static Dictionary<string, string> _fallbackCache;
        private static DateTime _fallbackLoadedAt;

        public List<AppSettingRow> ListSettings()
        {
            var list = new List<AppSettingRow>();
            using (IDataReader r = Db.ExecuteReader(Proc("dbo.usp_AppSetting_List")))
            {
                while (r.Read())
                {
                    list.Add(new AppSettingRow
                    {
                        Name = GetString(r, "Name"),
                        Value = GetString(r, "Value"),
                        Description = GetString(r, "Description"),
                        ModifiedBy = GetString(r, "ModifiedBy"),
                        ModifiedOn = GetNullableDate(r, "ModifiedOn")
                    });
                }
            }

            return list;
        }

        /// <summary>
        /// Settings are cached for five minutes in the ASP.NET cache (web sites) or a static dictionary (services and tools).
        /// </summary>
        public string GetSetting(string name, string defaultValue)
        {
            Dictionary<string, string> settings = LoadSettings();
            string value;
            return settings.TryGetValue(name, out value) ? value : defaultValue;
        }

        public int GetSetting(string name, int defaultValue)
        {
            int value;
            return int.TryParse(GetSetting(name, null), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : defaultValue;
        }

        public decimal GetSetting(string name, decimal defaultValue)
        {
            decimal value;
            return decimal.TryParse(GetSetting(name, null), NumberStyles.Number, CultureInfo.InvariantCulture, out value) ? value : defaultValue;
        }

        public bool GetSetting(string name, bool defaultValue)
        {
            bool value;
            return bool.TryParse(GetSetting(name, null), out value) ? value : defaultValue;
        }

        public void SaveSetting(string name, string value, string userName)
        {
            DbCommand cmd = Proc("dbo.usp_AppSetting_Save");
            In(cmd, "@Name", DbType.AnsiString, name);
            In(cmd, "@Value", DbType.String, value);
            In(cmd, "@UserName", DbType.String, userName);
            Db.ExecuteNonQuery(cmd);
            if (HttpRuntime.Cache != null)
            {
                HttpRuntime.Cache.Remove(SettingsCacheKey);
            }

            _fallbackCache = null;
        }

        public void Audit(string userName, string action, string entityType, string entityId, int? patientId, string details, string clientAddress)
        {
            DbCommand cmd = Proc("dbo.usp_Audit_Write");
            In(cmd, "@UserName", DbType.String, userName ?? "anonymous");
            In(cmd, "@Action", DbType.AnsiString, action);
            In(cmd, "@EntityType", DbType.AnsiString, entityType);
            In(cmd, "@EntityId", DbType.AnsiString, entityId);
            In(cmd, "@PatientId", DbType.Int32, patientId);
            In(cmd, "@Details", DbType.String, details != null && details.Length > 1000 ? details.Substring(0, 1000) : details);
            In(cmd, "@ClientAddress", DbType.AnsiString, clientAddress);
            Db.ExecuteNonQuery(cmd);
        }

        public int StartJob(string jobName)
        {
            DbCommand cmd = Proc("dbo.usp_Job_Start");
            In(cmd, "@JobName", DbType.AnsiString, jobName);
            In(cmd, "@MachineName", DbType.AnsiString, Environment.MachineName);
            Out(cmd, "@JobRunId", DbType.Int32, 4);
            Db.ExecuteNonQuery(cmd);
            return OutValue<int>(cmd, "@JobRunId");
        }

        public void FinishJob(int jobRunId, bool succeeded, string summary)
        {
            DbCommand cmd = Proc("dbo.usp_Job_Finish");
            In(cmd, "@JobRunId", DbType.Int32, jobRunId);
            In(cmd, "@Succeeded", DbType.Boolean, succeeded);
            In(cmd, "@Summary", DbType.String, summary != null && summary.Length > 1000 ? summary.Substring(0, 1000) : summary);
            Db.ExecuteNonQuery(cmd);
        }

        public ApiClientInfo ValidateApiKey(string apiKeyHash, string clientType)
        {
            DbCommand cmd = Proc("dbo.usp_ApiClient_Validate");
            In(cmd, "@ApiKeyHash", DbType.AnsiStringFixedLength, apiKeyHash);
            In(cmd, "@ClientType", DbType.AnsiString, clientType);
            using (IDataReader r = Db.ExecuteReader(cmd))
            {
                if (!r.Read())
                {
                    return null;
                }

                return new ApiClientInfo
                {
                    ApiClientId = GetInt(r, "ApiClientId"),
                    Name = GetString(r, "Name"),
                    ClientType = GetString(r, "ClientType"),
                    SiteId = GetNullableInt(r, "SiteId")
                };
            }
        }

        private Dictionary<string, string> LoadSettings()
        {
            Cache cache = HttpRuntime.AppDomainAppId != null ? HttpRuntime.Cache : null;
            if (cache != null)
            {
                var cached = cache[SettingsCacheKey] as Dictionary<string, string>;
                if (cached != null)
                {
                    return cached;
                }
            }
            else if (_fallbackCache != null && DateTime.Now - _fallbackLoadedAt < TimeSpan.FromMinutes(5))
            {
                return _fallbackCache;
            }

            lock (SettingsLock)
            {
                var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (AppSettingRow row in ListSettings())
                {
                    settings[row.Name] = row.Value;
                }

                if (cache != null)
                {
                    cache.Insert(SettingsCacheKey, settings, null, DateTime.Now.AddMinutes(5), Cache.NoSlidingExpiration);
                }
                else
                {
                    _fallbackCache = settings;
                    _fallbackLoadedAt = DateTime.Now;
                }

                return settings;
            }
        }
    }
}
