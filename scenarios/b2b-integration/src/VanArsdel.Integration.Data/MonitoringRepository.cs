using System;
using System.Collections.Generic;
using System.Data;
using VanArsdel.Integration.Domain.Sla;
using VanArsdel.Integration.Monitoring;

namespace VanArsdel.Integration.Data
{
    /// <summary>SLA definitions, holidays, alert state and alert log (dbo.SlaDefinition, dbo.Holiday, dbo.AlertState, dbo.AlertLog).</summary>
    public class MonitoringRepository : IAlertStateStore
    {
        private readonly IntegrationDatabase _db;

        public MonitoringRepository(IntegrationDatabase db)
        {
            _db = db;
        }

        public IList<SlaDefinition> SlaDefinitions()
        {
            var list = new List<SlaDefinition>();
            using (var cmd = _db.Procedure("dbo.usp_GetSlaDefinitions"))
            using (var r = _db.Database.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    list.Add(new SlaDefinition
                    {
                        Code = IntegrationDatabase.S(r, "Code"),
                        Kind = (SlaKind)IntegrationDatabase.I(r, "KindId"),
                        Description = IntegrationDatabase.S(r, "Description"),
                        PartnerId = IntegrationDatabase.S(r, "PartnerId"),
                        ThresholdMinutes = IntegrationDatabase.I(r, "ThresholdMinutes"),
                        ThresholdCount = IntegrationDatabase.I(r, "ThresholdCount"),
                        Severity = (AlertSeverity)IntegrationDatabase.I(r, "SeverityId"),
                        BusinessHoursOnly = IntegrationDatabase.B(r, "BusinessHoursOnly"),
                        NotifyGroup = IntegrationDatabase.S(r, "NotifyGroup"),
                        Enabled = IntegrationDatabase.B(r, "Enabled")
                    });
                }
            }

            return list;
        }

        public IList<DateTime> Holidays(int year)
        {
            var list = new List<DateTime>();
            using (var cmd = _db.Text("SELECT HolidayDate FROM dbo.Holiday WHERE YEAR(HolidayDate) IN (@Year, @Year + 1)"))
            {
                _db.In(cmd, "@Year", DbType.Int32, year);
                using (var r = _db.Database.ExecuteReader(cmd))
                {
                    while (r.Read())
                    {
                        list.Add(r.GetDateTime(0));
                    }
                }
            }

            return list;
        }

        public IDictionary<string, string> NotifyGroups()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = _db.Text("SELECT GroupName, MailAddresses FROM dbo.NotifyGroup"))
            using (var r = _db.Database.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    map[r.GetString(0)] = r.GetString(1);
                }
            }

            return map;
        }

        public IDictionary<string, AlertState> LoadOpen()
        {
            var map = new Dictionary<string, AlertState>(StringComparer.Ordinal);
            using (var cmd = _db.Text("SELECT AlertKey, FirstRaisedUtc, LastNotifiedUtc, NotifyCount, SeverityId, Subject, NotifyGroup FROM dbo.AlertState WHERE ClearedUtc IS NULL"))
            using (var r = _db.Database.ExecuteReader(cmd))
            {
                while (r.Read())
                {
                    var s = new AlertState
                    {
                        Key = IntegrationDatabase.S(r, "AlertKey"),
                        FirstRaisedUtc = IntegrationDatabase.D(r, "FirstRaisedUtc").GetValueOrDefault(),
                        LastNotifiedUtc = IntegrationDatabase.D(r, "LastNotifiedUtc").GetValueOrDefault(),
                        NotifyCount = IntegrationDatabase.I(r, "NotifyCount"),
                        Severity = (AlertSeverity)IntegrationDatabase.I(r, "SeverityId"),
                        Subject = IntegrationDatabase.S(r, "Subject"),
                        NotifyGroup = IntegrationDatabase.S(r, "NotifyGroup")
                    };
                    map[s.Key] = s;
                }
            }

            return map;
        }

        public void Save(AlertState state)
        {
            using (var cmd = _db.Procedure("dbo.usp_SaveAlertState"))
            {
                _db.In(cmd, "@AlertKey", DbType.String, state.Key);
                _db.In(cmd, "@FirstRaisedUtc", DbType.DateTime2, state.FirstRaisedUtc);
                _db.In(cmd, "@LastNotifiedUtc", DbType.DateTime2, state.LastNotifiedUtc);
                _db.In(cmd, "@NotifyCount", DbType.Int32, state.NotifyCount);
                _db.In(cmd, "@SeverityId", DbType.Int32, (int)state.Severity);
                _db.In(cmd, "@Subject", DbType.String, state.Subject);
                _db.In(cmd, "@NotifyGroup", DbType.String, state.NotifyGroup);
                _db.Database.ExecuteNonQuery(cmd);
            }
        }

        public void Close(string key, DateTime clearedUtc)
        {
            using (var cmd = _db.Text("UPDATE dbo.AlertState SET ClearedUtc = @ClearedUtc WHERE AlertKey = @AlertKey AND ClearedUtc IS NULL"))
            {
                _db.In(cmd, "@AlertKey", DbType.String, key);
                _db.In(cmd, "@ClearedUtc", DbType.DateTime2, clearedUtc);
                _db.Database.ExecuteNonQuery(cmd);
            }
        }

        public void LogAlert(Alert alert)
        {
            using (var cmd = _db.Procedure("dbo.usp_LogAlert"))
            {
                _db.In(cmd, "@AlertKey", DbType.String, alert.Key);
                _db.In(cmd, "@SlaCode", DbType.String, alert.SlaCode);
                _db.In(cmd, "@SeverityId", DbType.Int32, (int)alert.Severity);
                _db.In(cmd, "@PartnerId", DbType.String, alert.PartnerId);
                _db.In(cmd, "@Subject", DbType.String, alert.Subject);
                _db.In(cmd, "@Detail", DbType.String, alert.Detail);
                _db.In(cmd, "@RaisedUtc", DbType.DateTime2, alert.RaisedUtc);
                _db.Database.ExecuteNonQuery(cmd);
            }
        }

        public IList<Alert> RecentAlerts(int days)
        {
            var list = new List<Alert>();
            using (var cmd = _db.Text("SELECT TOP (500) AlertKey, SlaCode, SeverityId, PartnerId, Subject, Detail, RaisedUtc FROM dbo.AlertLog WHERE RaisedUtc >= DATEADD(DAY, -@Days, SYSUTCDATETIME()) ORDER BY RaisedUtc DESC"))
            {
                _db.In(cmd, "@Days", DbType.Int32, days);
                using (var r = _db.Database.ExecuteReader(cmd))
                {
                    while (r.Read())
                    {
                        list.Add(new Alert
                        {
                            Key = IntegrationDatabase.S(r, "AlertKey"),
                            SlaCode = IntegrationDatabase.S(r, "SlaCode"),
                            Severity = (AlertSeverity)IntegrationDatabase.I(r, "SeverityId"),
                            PartnerId = IntegrationDatabase.S(r, "PartnerId"),
                            Subject = IntegrationDatabase.S(r, "Subject"),
                            Detail = IntegrationDatabase.S(r, "Detail"),
                            RaisedUtc = IntegrationDatabase.D(r, "RaisedUtc").GetValueOrDefault()
                        });
                    }
                }
            }

            return list;
        }
    }
}
