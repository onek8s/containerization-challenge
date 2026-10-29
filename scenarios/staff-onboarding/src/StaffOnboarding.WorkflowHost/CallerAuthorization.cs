using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.ServiceModel;
using StaffOnboarding.Workflows.Activities;

namespace StaffOnboarding.WorkflowHost
{
    /// <summary>
    /// Only the portal application pool account (and local administrators on the control endpoint)
    /// may send messages to the workflows. Entries are DOMAIN\account or DOMAIN\group.
    /// </summary>
    public class CallerAuthorization : ServiceAuthorizationManager
    {
        private readonly HashSet<string> _allowed;

        public CallerAuthorization(IEnumerable<string> allowed)
        {
            _allowed = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);
        }

        protected override bool CheckAccessCore(OperationContext operationContext)
        {
            var security = operationContext.ServiceSecurityContext;
            var identity = security == null ? null : security.WindowsIdentity;
            if (identity == null || identity.IsAnonymous) return false;

            if (_allowed.Contains(identity.Name)) return true;

            var principal = new WindowsPrincipal(identity);
            if (_allowed.Any(principal.IsInRole)) return true;

            var isControl = operationContext.EndpointDispatcher.EndpointAddress.Uri.Scheme == Uri.UriSchemeNetPipe;
            if (isControl && principal.IsInRole(WindowsBuiltInRole.Administrator)) return true;

            EventLog.WriteEntry(HostLog.Source, "Rejected call from " + identity.Name + " to " +
                operationContext.EndpointDispatcher.EndpointAddress.Uri, EventLogEntryType.FailureAudit, 3201);
            return false;
        }
    }
}
