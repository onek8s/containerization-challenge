using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Runtime.Caching;
using System.Security.Principal;
using Contoso.Purchasing.Core;
using Contoso.Purchasing.Data;

namespace Contoso.Purchasing.Api.Infrastructure
{
    /// <summary>
    /// Builds the purchasing view of the calling Windows user: groups, display name, e-mail
    /// and the cost centers the user manages.
    /// </summary>
    public class UserDirectory
    {
        public const string OnBehalfOfHeader = "X-Purchasing-User";

        private static readonly MemoryCache Cache = new MemoryCache("Purchasing.Users");

        private readonly Database _db;

        public UserDirectory(Database db)
        {
            _db = db;
        }

        public UserContext Resolve(IPrincipal principal, string onBehalfOf)
        {
            var windows = principal as WindowsPrincipal;
            if (windows == null || !principal.Identity.IsAuthenticated)
            {
                throw new InvalidOperationException("Windows authentication is required.");
            }

            var caller = principal.Identity.Name;
            if (!string.IsNullOrEmpty(onBehalfOf) && !string.Equals(onBehalfOf, caller, StringComparison.OrdinalIgnoreCase))
            {
                if (!AppSettings.TrustedCallers.Contains(caller, StringComparer.OrdinalIgnoreCase))
                {
                    Trace.TraceWarning("{0} tried to call on behalf of {1} but is not a trusted caller.", caller, onBehalfOf);
                    throw new UnauthorizedAccessException(caller + " may not call on behalf of other users.");
                }
                return Cached(onBehalfOf, () => FromDirectory(onBehalfOf));
            }

            return Cached(caller, () => FromToken(windows));
        }

        private UserContext Cached(string account, Func<UserContext> build)
        {
            var cached = Cache.Get(account.ToUpperInvariant()) as UserContext;
            if (cached != null) return cached;

            var user = build();
            var catalog = new CatalogRepository(_db);
            user.ManagedCostCenters.AddRange(catalog.CostCenters()
                .Where(c => string.Equals(c.ManagerAccount, user.Account, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Code));

            Cache.Set(account.ToUpperInvariant(), user, DateTimeOffset.Now.AddMinutes(10));
            return user;
        }

        private static UserContext FromToken(WindowsPrincipal windows)
        {
            var user = new UserContext
            {
                Account = windows.Identity.Name,
                DisplayName = windows.Identity.Name,
                IsFinance = windows.IsInRole(AppSettings.FinanceGroup),
                IsDirector = windows.IsInRole(AppSettings.DirectorGroup),
                IsBuyer = windows.IsInRole(AppSettings.BuyerGroup),
                IsAdministrator = windows.IsInRole(AppSettings.AdminGroup)
            };

            using (var context = OpenContext(user.Account))
            using (var account = Find(context, user.Account))
            {
                if (account != null)
                {
                    user.DisplayName = account.DisplayName ?? user.Account;
                    user.Email = account.EmailAddress;
                }
            }

            return user;
        }

        private static UserContext FromDirectory(string accountName)
        {
            var user = new UserContext { Account = accountName, DisplayName = accountName };

            using (var context = OpenContext(accountName))
            using (var account = Find(context, accountName))
            {
                if (account == null)
                {
                    throw new UnauthorizedAccessException("Unknown user " + accountName + ".");
                }

                user.DisplayName = account.DisplayName ?? accountName;
                user.Email = account.EmailAddress;

                var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var group in account.GetAuthorizationGroups())
                {
                    if (!string.IsNullOrEmpty(group.SamAccountName)) groups.Add(group.SamAccountName);
                }

                user.IsFinance = groups.Contains(GroupName(AppSettings.FinanceGroup));
                user.IsDirector = groups.Contains(GroupName(AppSettings.DirectorGroup));
                user.IsBuyer = groups.Contains(GroupName(AppSettings.BuyerGroup));
                user.IsAdministrator = groups.Contains(GroupName(AppSettings.AdminGroup));
            }

            return user;
        }

        private static PrincipalContext OpenContext(string accountName)
        {
            var domain = accountName.Contains("\\") ? accountName.Substring(0, accountName.IndexOf('\\')) : null;
            if (string.Equals(domain, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            {
                return new PrincipalContext(ContextType.Machine);
            }

            return string.IsNullOrEmpty(AppSettings.DirectoryDomain)
                ? new PrincipalContext(ContextType.Domain)
                : new PrincipalContext(ContextType.Domain, AppSettings.DirectoryDomain);
        }

        private static UserPrincipal Find(PrincipalContext context, string accountName)
        {
            try
            {
                return UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, GroupName(accountName));
            }
            catch (PrincipalServerDownException ex)
            {
                Trace.TraceWarning("Directory lookup failed for {0}: {1}", accountName, ex.Message);
                return null;
            }
            catch (PrincipalOperationException ex)
            {
                Trace.TraceWarning("Directory lookup failed for {0}: {1}", accountName, ex.Message);
                return null;
            }
        }

        private static string GroupName(string qualified)
        {
            var slash = qualified.IndexOf('\\');
            return slash >= 0 ? qualified.Substring(slash + 1) : qualified;
        }
    }
}
