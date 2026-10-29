using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Web;
using System.Web.Caching;

namespace Tailspin.CustomerCare.Web.Security
{
    public class DirectoryEntryInfo
    {
        public string AccountName { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }
    }

    /// <summary>Active Directory lookups for display names, e-mail addresses and group members.</summary>
    public static class UserDirectory
    {
        private static readonly TimeSpan CacheTime = TimeSpan.FromMinutes(30);

        private static string Domain
        {
            get { return ConfigurationManager.AppSettings["CustomerCare:Domain"] ?? "CORP"; }
        }

        public static DirectoryEntryInfo Find(string accountName)
        {
            if (string.IsNullOrEmpty(accountName)) return null;

            return Cached("dir:user:" + accountName.ToLowerInvariant(), () =>
            {
                try
                {
                    using (var ctx = new PrincipalContext(ContextType.Domain, Domain))
                    using (var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, accountName))
                    {
                        if (user == null) return new DirectoryEntryInfo { AccountName = accountName, DisplayName = accountName };
                        return new DirectoryEntryInfo { AccountName = accountName, DisplayName = user.DisplayName, Email = user.EmailAddress };
                    }
                }
                catch (Exception ex) when (ex is PrincipalException || ex is System.Runtime.InteropServices.COMException)
                {
                    Trace.TraceWarning("Directory lookup for {0} failed: {1}", accountName, ex.Message);
                    return new DirectoryEntryInfo { AccountName = accountName, DisplayName = accountName };
                }
            });
        }

        /// <summary>Members of an application group (recursive), used for assignment lists and e-mail notifications.</summary>
        public static IList<DirectoryEntryInfo> Members(CareRole role)
        {
            var group = CareGroups.GroupFor(role);
            return Cached("dir:group:" + group.ToLowerInvariant(), () =>
            {
                var list = new List<DirectoryEntryInfo>();
                try
                {
                    using (var ctx = new PrincipalContext(ContextType.Domain, Domain))
                    using (var g = GroupPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, StripDomain(group)))
                    {
                        if (g == null) return list;
                        foreach (var p in g.GetMembers(true).OfType<UserPrincipal>())
                        {
                            if (p.Enabled == false) continue;
                            list.Add(new DirectoryEntryInfo
                            {
                                AccountName = Domain + "\\" + p.SamAccountName,
                                DisplayName = p.DisplayName ?? p.SamAccountName,
                                Email = p.EmailAddress
                            });
                        }
                    }
                }
                catch (Exception ex) when (ex is PrincipalException || ex is System.Runtime.InteropServices.COMException)
                {
                    Trace.TraceWarning("Reading members of {0} failed: {1}", group, ex.Message);
                }

                return list.OrderBy(x => x.DisplayName).ToList();
            });
        }

        public static string DisplayName(string accountName)
        {
            if (string.IsNullOrEmpty(accountName)) return string.Empty;
            var e = Find(accountName);
            return e != null && !string.IsNullOrEmpty(e.DisplayName) ? e.DisplayName : accountName;
        }

        private static string StripDomain(string name)
        {
            var i = name.IndexOf('\\');
            return i >= 0 ? name.Substring(i + 1) : name;
        }

        private static T Cached<T>(string key, Func<T> load) where T : class
        {
            var value = HttpRuntime.Cache[key] as T;
            if (value != null) return value;

            value = load();
            HttpRuntime.Cache.Insert(key, value, null, DateTime.UtcNow.Add(CacheTime), Cache.NoSlidingExpiration);
            return value;
        }
    }
}
