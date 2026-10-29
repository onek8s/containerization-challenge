using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using Tailspin.CustomerCare.Domain;

namespace Tailspin.CustomerCare.Data
{
    /// <summary>
    /// Queues, categories, routing rules, SLA targets and holidays. These change a few times a year, so they are
    /// read once and kept in memory. The admin pages call <see cref="Invalidate"/> after saving.
    /// </summary>
    public static class ReferenceData
    {
        private const string Prefix = "care:ref:";
        private static readonly object Sync = new object();

        public static IList<Queue> Queues
        {
            get { return Get("queues", db => db.Queues.AsNoTracking().OrderBy(q => q.SortOrder).ThenBy(q => q.Name).ToList()); }
        }

        public static IList<Queue> ActiveQueues
        {
            get { return Queues.Where(q => q.IsActive).ToList(); }
        }

        public static IList<Category> Categories
        {
            get { return Get("categories", db => db.Categories.AsNoTracking().OrderBy(c => c.Name).ToList()); }
        }

        public static IList<RoutingRule> RoutingRules
        {
            get { return Get("rules", db => db.RoutingRules.AsNoTracking().ToList()); }
        }

        public static IList<SlaTarget> SlaTargets
        {
            get { return Get("sla", db => db.SlaTargets.AsNoTracking().ToList()); }
        }

        public static IList<Holiday> Holidays
        {
            get { return Get("holidays", db => db.Holidays.AsNoTracking().OrderBy(h => h.Date).ToList()); }
        }

        public static Queue QueueByCode(string code)
        {
            return Queues.FirstOrDefault(q => string.Equals(q.Code, code, StringComparison.OrdinalIgnoreCase));
        }

        public static Queue QueueById(int id)
        {
            return Queues.FirstOrDefault(q => q.Id == id);
        }

        public static string CategoryName(string code)
        {
            var c = Categories.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
            return c != null ? c.Name : code;
        }

        public static BusinessCalendar Calendar
        {
            get
            {
                return GetObject("calendar", () => new BusinessCalendar(CareSettings.TimeZone, CareSettings.OfficeOpens, CareSettings.OfficeCloses,
                    Holidays.Select(h => h.Date)));
            }
        }

        public static SlaPolicy SlaPolicy
        {
            get { return GetObject("slapolicy", () => new SlaPolicy(Calendar, SlaTargets)); }
        }

        public static CaseRouter Router
        {
            get { return GetObject("router", () => new CaseRouter(RoutingRules, Categories, CareSettings.DefaultQueue)); }
        }

        public static void Invalidate()
        {
            lock (Sync)
            {
                foreach (var key in MemoryCache.Default.Select(kv => kv.Key).Where(k => k.StartsWith(Prefix, StringComparison.Ordinal)).ToList())
                {
                    MemoryCache.Default.Remove(key);
                }
            }
        }

        private static IList<T> Get<T>(string name, Func<CareDbContext, List<T>> load)
        {
            return GetObject(name, () =>
            {
                using (var db = new CareDbContext())
                {
                    return (IList<T>)load(db).AsReadOnly();
                }
            });
        }

        private static T GetObject<T>(string name, Func<T> factory) where T : class
        {
            var key = Prefix + name;
            var value = MemoryCache.Default.Get(key) as T;
            if (value != null) return value;

            lock (Sync)
            {
                value = MemoryCache.Default.Get(key) as T;
                if (value != null) return value;

                value = factory();
                MemoryCache.Default.Set(key, value, DateTimeOffset.UtcNow.AddMinutes(CareSettings.ReferenceDataCacheMinutes));
                return value;
            }
        }
    }
}
