using System;
using System.Collections.Generic;

namespace StaffOnboarding.Core.Provisioning
{
    /// <summary>
    /// Commands accepted by the directory provisioning service (idm01). Every command carries the
    /// request number and step so a repeated call after a timeout does not run twice.
    /// </summary>
    [Serializable]
    public abstract class ProvisioningCommand
    {
        public string RequestNumber { get; set; }
        public string Step { get; set; }
    }

    [Serializable]
    public class CreateAccountCommand : ProvisioningCommand
    {
        public CreateAccountCommand()
        {
            Groups = new List<string>();
        }

        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PreferredName { get; set; }
        public string EmployeeId { get; set; }
        public string MobilePhone { get; set; }
        public string Department { get; set; }
        public string JobRole { get; set; }
        public string Title { get; set; }
        public string Office { get; set; }
        public string CostCenter { get; set; }
        public string ManagerAccount { get; set; }
        public string EmploymentType { get; set; }
        public DateTime? AccountExpiresUtc { get; set; }
        public string TargetOu { get; set; }
        public List<string> Groups { get; set; }
        public string DepartmentShare { get; set; }

        /// <summary>The account stays disabled until <see cref="EnableAccountCommand"/> on the start date.</summary>
        public bool CreateDisabled { get; set; }
    }

    [Serializable]
    public class EnableAccountCommand : ProvisioningCommand
    {
        public string SamAccountName { get; set; }
    }

    [Serializable]
    public class UpdateAccessCommand : ProvisioningCommand
    {
        public UpdateAccessCommand()
        {
            AddGroups = new List<string>();
            RemoveGroups = new List<string>();
        }

        public string SamAccountName { get; set; }
        public string Department { get; set; }
        public string Title { get; set; }
        public string Office { get; set; }
        public string CostCenter { get; set; }
        public string ManagerAccount { get; set; }
        public string TargetOu { get; set; }
        public DateTime? AccountExpiresUtc { get; set; }
        public List<string> AddGroups { get; set; }
        public List<string> RemoveGroups { get; set; }
    }

    [Serializable]
    public class DisableAccountCommand : ProvisioningCommand
    {
        public DisableAccountCommand()
        {
            RemoveGroups = new List<string>();
        }

        public string SamAccountName { get; set; }
        public string TargetOu { get; set; }
        public List<string> RemoveGroups { get; set; }
        public string Reason { get; set; }
    }

    [Serializable]
    public class ArchiveAccountCommand : ProvisioningCommand
    {
        public string SamAccountName { get; set; }

        /// <summary>Account that is granted read access to the archived home folder.</summary>
        public string HandoverTo { get; set; }
    }

    [Serializable]
    public class ProvisioningResult
    {
        public ProvisioningResult()
        {
            Messages = new List<string>();
        }

        public bool Succeeded { get; set; }

        /// <summary>True when the failure is temporary (domain controller or file server unreachable) and the step can be retried.</summary>
        public bool Retryable { get; set; }

        public bool AlreadyDone { get; set; }
        public string SamAccountName { get; set; }
        public string UserPrincipalName { get; set; }
        public string Mail { get; set; }
        public string DistinguishedName { get; set; }
        public string HomeDirectory { get; set; }
        public string DomainController { get; set; }
        public List<string> Messages { get; set; }

        public static ProvisioningResult Fail(string message, bool retryable)
        {
            var r = new ProvisioningResult { Succeeded = false, Retryable = retryable };
            r.Messages.Add(message);
            return r;
        }
    }

    [Serializable]
    public class AccountSummary
    {
        public AccountSummary()
        {
            Groups = new List<string>();
        }

        public string SamAccountName { get; set; }
        public string DisplayName { get; set; }
        public string Mail { get; set; }
        public string DistinguishedName { get; set; }
        public string Department { get; set; }
        public string Title { get; set; }
        public string ManagerAccount { get; set; }
        public bool Enabled { get; set; }
        public List<string> Groups { get; set; }
    }
}
