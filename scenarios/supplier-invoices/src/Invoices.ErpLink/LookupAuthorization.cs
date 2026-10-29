using System;
using System.Linq;
using System.Net;
using System.Runtime.Remoting.Channels;
using System.Security.Principal;
using Wingtip.Invoices.Infrastructure.Configuration;
using Wingtip.Invoices.Infrastructure.Diagnostics;

namespace Wingtip.Invoices.ErpLink
{
    /// <summary>
    /// Authorization module of the secure tcp channel: only the processor service account
    /// (appSettings LookupCallers) may connect.
    /// </summary>
    public class LookupAuthorization : IAuthorizeRemotingConnection
    {
        public bool IsConnectingEndPointAuthorized(EndPoint endPoint)
        {
            return true;
        }

        public bool IsConnectingIdentityAuthorized(IIdentity identity)
        {
            var windows = identity as WindowsIdentity;
            if (windows == null || windows.IsAnonymous) return false;

            var principal = new WindowsPrincipal(windows);
            var allowed = HubSettings.Get("LookupCallers", string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim());
            if (allowed.Any(a => string.Equals(a, windows.Name, StringComparison.OrdinalIgnoreCase) || SafeIsInRole(principal, a)))
                return true;

            HubLog.Warning(HubLog.Events.ControlDenied, "ERP lookup connection from {0} was refused.", windows.Name);
            return false;
        }

        private static bool SafeIsInRole(WindowsPrincipal principal, string role)
        {
            try
            {
                return principal.IsInRole(role);
            }
            catch (SystemException)
            {
                return false;
            }
        }
    }
}
