using System;
using System.Linq;
using System.Security.Principal;
using System.ServiceModel;
using Wingtip.Invoices.Infrastructure.Configuration;
using Wingtip.Invoices.Infrastructure.Diagnostics;

namespace Wingtip.Invoices.Processor.Control
{
    /// <summary>
    /// Allows the control endpoint only to the accounts and groups in appSettings ControlCallers
    /// (the application pool account of the web application and the AP administrators).
    /// </summary>
    public class ControlAuthorization : ServiceAuthorizationManager
    {
        protected override bool CheckAccessCore(OperationContext operationContext)
        {
            var identity = operationContext.ServiceSecurityContext == null ? null : operationContext.ServiceSecurityContext.WindowsIdentity;
            if (identity == null || identity.IsAnonymous) return false;

            var principal = new WindowsPrincipal(identity);
            var allowed = HubSettings.Get("ControlCallers", string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim());

            foreach (var entry in allowed)
            {
                if (string.Equals(identity.Name, entry, StringComparison.OrdinalIgnoreCase)) return true;
                try
                {
                    if (principal.IsInRole(entry)) return true;
                }
                catch (SystemException)
                {
                    // Not a group name.
                }
            }

            HubLog.Warning(HubLog.Events.ControlDenied, "Control call {0} from {1} was denied.", operationContext.IncomingMessageHeaders.Action, identity.Name);
            return false;
        }
    }
}
