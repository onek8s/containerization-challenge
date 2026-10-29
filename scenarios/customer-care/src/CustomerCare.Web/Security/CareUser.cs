using System;
using System.Security.Principal;
using System.Web;

namespace Tailspin.CustomerCare.Web.Security
{
    [Serializable]
    public class CareUserProfile
    {
        public string AccountName { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public bool IsAgent { get; set; }
        public bool IsSupervisor { get; set; }
        public bool IsAdministrator { get; set; }
    }

    /// <summary>
    /// The signed-in Windows user. Group membership comes from the Windows token (IIS Windows authentication);
    /// supervisors and administrators are implicitly agents.
    /// </summary>
    public static class CareUser
    {
        private const string SessionKey = "CareUser.Profile";

        public static CareUserProfile Current
        {
            get
            {
                var ctx = HttpContext.Current;
                if (ctx == null) return null;
                return Get(new HttpContextWrapper(ctx));
            }
        }

        public static string AccountName
        {
            get
            {
                var ctx = HttpContext.Current;
                return ctx != null && ctx.User != null ? ctx.User.Identity.Name : null;
            }
        }

        public static CareUserProfile Get(HttpContextBase ctx)
        {
            if (ctx.Session != null)
            {
                var cached = ctx.Session[SessionKey] as CareUserProfile;
                if (cached != null && string.Equals(cached.AccountName, ctx.User.Identity.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return cached;
                }
            }

            return LoadProfile(ctx);
        }

        public static CareUserProfile LoadProfile(HttpContextBase ctx)
        {
            var user = ctx.User;
            var name = user.Identity.Name;
            var entry = UserDirectory.Find(name);

            var profile = new CareUserProfile
            {
                AccountName = name,
                DisplayName = entry != null && !string.IsNullOrEmpty(entry.DisplayName) ? entry.DisplayName : name,
                Email = entry != null ? entry.Email : null,
                IsAdministrator = IsInGroup(user, CareRole.Administrator),
                IsSupervisor = IsInGroup(user, CareRole.Supervisor)
            };
            profile.IsSupervisor |= profile.IsAdministrator;
            profile.IsAgent = profile.IsSupervisor || IsInGroup(user, CareRole.Agent);

            if (ctx.Session != null) ctx.Session[SessionKey] = profile;
            return profile;
        }

        public static bool IsIn(HttpContextBase ctx, CareRole role)
        {
            var p = Get(ctx);
            switch (role)
            {
                case CareRole.Administrator: return p.IsAdministrator;
                case CareRole.Supervisor: return p.IsSupervisor;
                default: return p.IsAgent;
            }
        }

        private static bool IsInGroup(IPrincipal user, CareRole role)
        {
            var group = CareGroups.GroupFor(role);
            try
            {
                return user.IsInRole(group);
            }
            catch (SystemException)
            {
                // Group could not be translated (domain controller unreachable or group renamed).
                return false;
            }
        }

        /// <summary>CORP\hlaine -> hlaine, for compact display in lists.</summary>
        public static string ShortName(string accountName)
        {
            if (string.IsNullOrEmpty(accountName)) return string.Empty;
            var i = accountName.IndexOf('\\');
            return i >= 0 ? accountName.Substring(i + 1) : accountName;
        }
    }
}
