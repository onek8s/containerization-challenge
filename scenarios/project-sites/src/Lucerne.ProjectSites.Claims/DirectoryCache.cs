using System;
using System.Collections.Generic;
using System.Linq;
using Lucerne.ProjectSites.Domain;
using Microsoft.SharePoint.Administration;

namespace Lucerne.ProjectSites.Claims
{
    /// <summary>
    /// Office and discipline of every employee, kept in the farm configuration database so the claims provider
    /// can answer without calling the hub (claims augmentation runs on every sign-in, on every server).
    /// Refreshed by the DirectoryCacheJob from the hub Staff Directory.
    /// </summary>
    public class DirectoryCache : SPPersistedObject
    {
        public const string ObjectName = "LucerneDirectoryCache";

        [Persisted]
        private Dictionary<string, string> _entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        [Persisted]
        private DateTime _refreshedUtc;

        public DirectoryCache()
        {
        }

        public DirectoryCache(SPPersistedObject parent)
            : base(ObjectName, parent)
        {
        }

        public DateTime RefreshedUtc
        {
            get { return _refreshedUtc; }
        }

        public int Count
        {
            get { return _entries.Count; }
        }

        public static DirectoryCache Local
        {
            get { return SPFarm.Local.GetChild<DirectoryCache>(ObjectName); }
        }

        public static DirectoryCache EnsureLocal()
        {
            var cache = Local;
            if (cache == null)
            {
                cache = new DirectoryCache(SPFarm.Local);
                cache.Update();
            }

            return cache;
        }

        /// <summary>Entry format: office|discipline|display name.</summary>
        public bool TryGet(string accountName, out string office, out string discipline, out string displayName)
        {
            office = discipline = displayName = null;
            string value;
            if (string.IsNullOrEmpty(accountName) || !_entries.TryGetValue(accountName, out value))
            {
                return false;
            }

            var parts = value.Split('|');
            office = parts[0];
            discipline = parts.Length > 1 ? parts[1] : null;
            displayName = parts.Length > 2 ? parts[2] : accountName;
            return true;
        }

        public void Replace(IEnumerable<StaffMember> staff)
        {
            _entries = staff
                .Where(s => s.Active && !string.IsNullOrEmpty(s.AccountName))
                .GroupBy(s => s.AccountName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().OfficeCode + "|" + g.First().DisciplineCode + "|" + g.First().DisplayName, StringComparer.OrdinalIgnoreCase);
            _refreshedUtc = DateTime.UtcNow;
        }
    }
}
