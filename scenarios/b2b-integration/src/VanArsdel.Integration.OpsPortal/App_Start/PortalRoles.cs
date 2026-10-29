using System.Configuration;
using System.Security.Principal;

namespace VanArsdel.Integration.OpsPortal
{
    /// <summary>
    /// Windows groups of the portal. Viewers see everything; operators resume, terminate and resubmit and switch
    /// ports; integration admins also change parties, partner status and cross references.
    /// </summary>
    public static class PortalRoles
    {
        public static string Viewers
        {
            get { return Group("Portal.Group.Viewers", @"CORP\B2B-Viewers"); }
        }

        public static string Operators
        {
            get { return Group("Portal.Group.Operators", @"CORP\BizTalk Operators"); }
        }

        public static string Admins
        {
            get { return Group("Portal.Group.Admins", @"CORP\B2B-IntegrationAdmins"); }
        }

        public static bool CanView(IPrincipal user)
        {
            return CanOperate(user) || user.IsInRole(Viewers);
        }

        public static bool CanOperate(IPrincipal user)
        {
            return IsAdmin(user) || user.IsInRole(Operators);
        }

        public static bool IsAdmin(IPrincipal user)
        {
            return user.IsInRole(Admins);
        }

        private static string Group(string key, string fallback)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
    }
}
