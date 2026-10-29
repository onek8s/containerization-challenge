using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ProductReturns.Portal.Services
{
    public static class StaffRoles
    {
        public const string AnyStaff = "AnyStaff";
        public const string Agent = "Agent";
        public const string Supervisor = "Supervisor";
    }

    public class StaffUser
    {
        public string AccountName { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
    }

    public class StaffDirectoryOptions
    {
        /// <summary>"ActiveDirectory" (default) or "Configured" for development.</summary>
        public string Mode { get; set; } = "ActiveDirectory";

        public string Domain { get; set; } = "corp.local";

        public string NetbiosDomain { get; set; } = "CORP";

        /// <summary>Optional OU to search, e.g. OU=Staff,DC=corp,DC=local.</summary>
        public string SearchBase { get; set; }

        public string AgentGroup { get; set; } = "Returns-Agents";

        public string SupervisorGroup { get; set; } = "Returns-Supervisors";

        /// <summary>Development users when Mode is Configured: account name, password, roles.</summary>
        public List<ConfiguredUser> Users { get; set; } = new List<ConfiguredUser>();
    }

    public class ConfiguredUser
    {
        public string AccountName { get; set; }
        public string Password { get; set; }
        public string DisplayName { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
    }

    public interface IStaffDirectory
    {
        StaffUser SignIn(string accountName, string password);
    }

    /// <summary>Checks the password against the domain and maps the returns AD groups to portal roles.</summary>
    public class ActiveDirectoryStaffDirectory : IStaffDirectory
    {
        private readonly StaffDirectoryOptions _options;
        private readonly ILogger<ActiveDirectoryStaffDirectory> _logger;

        public ActiveDirectoryStaffDirectory(IOptions<StaffDirectoryOptions> options, ILogger<ActiveDirectoryStaffDirectory> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public StaffUser SignIn(string accountName, string password)
        {
            var samAccountName = StripDomain(accountName);
            if (string.IsNullOrEmpty(samAccountName) || string.IsNullOrEmpty(password))
            {
                return null;
            }

            using (var context = string.IsNullOrEmpty(_options.SearchBase)
                ? new PrincipalContext(ContextType.Domain, _options.Domain)
                : new PrincipalContext(ContextType.Domain, _options.Domain, _options.SearchBase))
            {
                if (!context.ValidateCredentials(samAccountName, password, ContextOptions.Negotiate | ContextOptions.Signing | ContextOptions.Sealing))
                {
                    _logger.LogWarning("Sign-in failed for {Account}", samAccountName);
                    return null;
                }

                using (var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, samAccountName))
                {
                    if (user == null || user.Enabled == false)
                    {
                        return null;
                    }

                    var groups = user.GetAuthorizationGroups().Select(g => g.SamAccountName).Where(n => n != null).ToList();
                    var staff = new StaffUser
                    {
                        AccountName = _options.NetbiosDomain + "\\" + user.SamAccountName,
                        DisplayName = user.DisplayName ?? user.SamAccountName,
                        Email = user.EmailAddress
                    };

                    if (groups.Contains(_options.SupervisorGroup, StringComparer.OrdinalIgnoreCase))
                    {
                        staff.Roles.Add(StaffRoles.Supervisor);
                        staff.Roles.Add(StaffRoles.Agent);
                    }
                    else if (groups.Contains(_options.AgentGroup, StringComparer.OrdinalIgnoreCase))
                    {
                        staff.Roles.Add(StaffRoles.Agent);
                    }

                    _logger.LogInformation("{Account} signed in with roles {Roles}", staff.AccountName, string.Join(",", staff.Roles));
                    return staff;
                }
            }
        }

        private static string StripDomain(string accountName)
        {
            if (string.IsNullOrWhiteSpace(accountName))
            {
                return null;
            }

            var name = accountName.Trim();
            var slash = name.IndexOf('\\');
            if (slash >= 0)
            {
                return name.Substring(slash + 1);
            }

            var at = name.IndexOf('@');
            return at > 0 ? name.Substring(0, at) : name;
        }
    }

    /// <summary>Users from configuration, for development without a domain controller.</summary>
    public class ConfiguredStaffDirectory : IStaffDirectory
    {
        private readonly StaffDirectoryOptions _options;

        public ConfiguredStaffDirectory(IOptions<StaffDirectoryOptions> options)
        {
            _options = options.Value;
        }

        public StaffUser SignIn(string accountName, string password)
        {
            var user = _options.Users.FirstOrDefault(u => string.Equals(u.AccountName, accountName?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (user == null || user.Password != password)
            {
                return null;
            }

            return new StaffUser { AccountName = user.AccountName, DisplayName = user.DisplayName ?? user.AccountName, Roles = user.Roles.ToList() };
        }
    }
}
