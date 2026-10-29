using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Caching;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Scheduling;

namespace Southridge.Clinic.StaffWeb
{
    /// <summary>ASP.NET cache for reference lists used in drop-downs. Admin pages call Invalidate after edits.</summary>
    public static class StaffCache
    {
        private const string Prefix = "staff:";

        public static IList<ListItemData> Sites()
        {
            return Get("sites", () => ClinicLookups.Sites().Select(s => new ListItemData(s.SiteId.ToString(), s.Name)).ToList());
        }

        public static IList<ListItemData> Providers(int? siteId)
        {
            return Get("providers:" + siteId, () => ClinicLookups.Providers(siteId, null)
                .Select(p => new ListItemData(p.ProviderId.ToString(), p.LastName + ", " + p.FirstName + (p.Credentials == null ? string.Empty : " " + p.Credentials)))
                .ToList());
        }

        public static IList<ListItemData> AppointmentTypes()
        {
            return Get("types", () => ClinicLookups.AppointmentTypes(false)
                .Select(t => new ListItemData(t.AppointmentTypeId.ToString(), t.Name + " (" + t.DurationMinutes + " min)"))
                .ToList());
        }

        public static IList<ListItemData> Payers()
        {
            return Get("payers", delegate
            {
                using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
                {
                    return db.Payers.Where(p => p.IsActive).OrderBy(p => p.Name)
                        .Select(p => new ListItemData(p.PayerId.ToString(), p.Name)).ToList();
                }
            });
        }

        public static IList<ListItemData> Specialties()
        {
            return Get("specialties", delegate
            {
                using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
                {
                    return db.Specialties.OrderBy(s => s.Name).Select(s => new ListItemData(s.SpecialtyCode, s.Name)).ToList();
                }
            });
        }

        public static IList<ListItemData> ProcedureCodes()
        {
            return Get("cpt", delegate
            {
                using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
                {
                    return db.ProcedureCodes.Where(p => p.IsActive).OrderBy(p => p.Category).ThenBy(p => p.CptCode)
                        .Select(p => new ListItemData(p.CptCode, p.CptCode + "  " + p.Description)).ToList();
                }
            });
        }

        /// <summary>Dashboard figures are expensive; they are cached for a minute per site.</summary>
        public static int UnbilledEncounters()
        {
            return Get("unbilled", () => new BillingRepository().UnbilledEncounters(3).Count, TimeSpan.FromMinutes(5));
        }

        public static void Invalidate()
        {
            var keys = new List<string>();
            foreach (System.Collections.DictionaryEntry entry in HttpRuntime.Cache)
            {
                string key = (string)entry.Key;
                if (key.StartsWith(Prefix, StringComparison.Ordinal))
                {
                    keys.Add(key);
                }
            }

            foreach (string key in keys)
            {
                HttpRuntime.Cache.Remove(key);
            }

            ClinicLookups.Invalidate();
        }

        private static T Get<T>(string key, Func<T> load)
        {
            return Get(key, load, TimeSpan.FromMinutes(20));
        }

        private static T Get<T>(string key, Func<T> load, TimeSpan duration)
        {
            object value = HttpRuntime.Cache[Prefix + key];
            if (value == null)
            {
                value = load();
                HttpRuntime.Cache.Insert(Prefix + key, value, null, DateTime.UtcNow.Add(duration), Cache.NoSlidingExpiration, CacheItemPriority.Normal, null);
            }

            return (T)value;
        }
    }

    [Serializable]
    public class ListItemData
    {
        public ListItemData(string value, string text)
        {
            Value = value;
            Text = text;
        }

        public string Value { get; private set; }

        public string Text { get; private set; }
    }
}
