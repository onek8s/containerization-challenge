using System;
using System.DirectoryServices.AccountManagement;
using System.Security.Principal;
using System.ServiceModel;
using CreditDesk.Core;

namespace CreditDesk.CreditService
{
    /// <summary>The Windows user calling the service (the person signed in to the intranet site).</summary>
    internal sealed class Caller
    {
        private Caller(WindowsIdentity identity)
        {
            Identity = identity;
            var principal = new WindowsPrincipal(identity);
            Roles = ApproverRoles.None;
            if (principal.IsInRole(CreditServiceSettings.SalesRepGroup)) Roles |= ApproverRoles.SalesRep;
            if (principal.IsInRole(CreditServiceSettings.CreditControllerGroup)) Roles |= ApproverRoles.CreditController;
            if (principal.IsInRole(CreditServiceSettings.FinanceManagerGroup)) Roles |= ApproverRoles.FinanceManager;
            if (principal.IsInRole(CreditServiceSettings.CreditCommitteeGroup)) Roles |= ApproverRoles.CreditCommittee;
        }

        public WindowsIdentity Identity { get; }
        public string AccountName => Identity.Name;
        public ApproverRoles Roles { get; }
        public bool IsApprover => (Roles & ~ApproverRoles.SalesRep) != ApproverRoles.None;

        public static Caller Current
        {
            get
            {
                var identity = ServiceSecurityContext.Current?.WindowsIdentity;
                if (identity == null || identity.IsAnonymous || !identity.IsAuthenticated)
                    throw new FaultException<Contracts.CreditFault>(
                        new Contracts.CreditFault("Windows authentication is required."), "Access denied");
                return new Caller(identity);
            }
        }

        public void Require(ApproverRoles anyOf)
        {
            if ((Roles & anyOf) == ApproverRoles.None)
            {
                Audit.Write(AccountName + " was denied access (requires " + anyOf + ").", System.Diagnostics.EventLogEntryType.FailureAudit, 4001);
                throw new FaultException<Contracts.CreditFault>(
                    new Contracts.CreditFault("You are not a member of a Credit Desk group that allows this action."), "Access denied");
            }
        }

        /// <summary>Display name and job title from Active Directory, used for letter signatures.</summary>
        public void LookupDirectory(out string displayName, out string title)
        {
            displayName = AccountName;
            title = "";
            try
            {
                using (var context = new PrincipalContext(ContextType.Domain))
                using (var user = UserPrincipal.FindByIdentity(context, IdentityType.Sid, Identity.User.Value))
                {
                    if (user == null) return;
                    displayName = string.IsNullOrWhiteSpace(user.DisplayName) ? AccountName : user.DisplayName;
                    var entry = user.GetUnderlyingObject() as System.DirectoryServices.DirectoryEntry;
                    var value = entry?.Properties["title"]?.Value as string;
                    if (!string.IsNullOrWhiteSpace(value)) title = value;
                }
            }
            catch (PrincipalServerDownException)
            {
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException || ex is UnauthorizedAccessException)
            {
            }
        }
    }
}
