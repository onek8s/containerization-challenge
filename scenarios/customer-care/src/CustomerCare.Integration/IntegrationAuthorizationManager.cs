using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.ServiceModel;

namespace Tailspin.CustomerCare.Integration
{
    /// <summary>
    /// Only members of the integration group (service accounts of the billing and outage systems,
    /// partner gateway) may call the service. Metadata requests are allowed for everybody who is signed in.
    /// </summary>
    public class IntegrationAuthorizationManager : ServiceAuthorizationManager
    {
        protected override bool CheckAccessCore(OperationContext operationContext)
        {
            var action = operationContext.IncomingMessageHeaders.Action ?? string.Empty;
            if (action.StartsWith("http://schemas.xmlsoap.org/ws/2004/09/transfer", StringComparison.Ordinal)) return true;

            var identity = operationContext.ServiceSecurityContext != null ? operationContext.ServiceSecurityContext.WindowsIdentity : null;
            if (identity == null || identity.IsAnonymous) return false;

            var groups = (ConfigurationManager.AppSettings["CustomerCare:IntegrationGroups"] ?? @"CORP\CC-Integration")
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(g => g.Trim());

            var principal = new WindowsPrincipal(identity);
            if (groups.Any(principal.IsInRole)) return true;

            Trace.TraceWarning("Integration call {0} by {1} was refused.", action, identity.Name);
            return false;
        }
    }
}
