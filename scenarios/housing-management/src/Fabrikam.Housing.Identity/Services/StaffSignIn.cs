using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Fabrikam.Housing.Hosting.Security;
using Fabrikam.Housing.Identity.Data;
using Fabrikam.Housing.Identity.Ldap;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fabrikam.Housing.Identity.Services
{
    /// <summary>
    /// Staff accounts live in Active Directory. On each sign-in the local account is created or refreshed
    /// from the directory so roles follow AD group membership.
    /// </summary>
    public class StaffSignIn
    {
        private readonly IStaffDirectory _directory;
        private readonly UserManager<ApplicationUser> _users;
        private readonly LdapOptions _options;
        private readonly ILogger<StaffSignIn> _logger;

        public StaffSignIn(IStaffDirectory directory, UserManager<ApplicationUser> users, IOptions<LdapOptions> options, ILogger<StaffSignIn> logger)
        {
            _directory = directory;
            _users = users;
            _options = options.Value;
            _logger = logger;
        }

        public bool IsStaffName(string userName) => StaffAccountName.Parse(userName, _options) != null;

        public async Task<ApplicationUser> AuthenticateAsync(string userName, string password)
        {
            var account = StaffAccountName.Parse(userName, _options);
            var entry = _directory.Authenticate(account, password);
            if (entry == null) return null;

            var roles = entry.Groups
                .Where(g => _options.GroupRoles.ContainsKey(g))
                .Select(g => _options.GroupRoles[g])
                .Distinct()
                .ToList();
            if (roles.Count == 0)
            {
                _logger.LogWarning("{Account} is not in any housing group", account);
                return null;
            }

            var loginName = _options.Domain + "\\" + entry.SamAccountName.ToLowerInvariant();
            var user = await _users.FindByNameAsync(loginName);
            if (user == null)
            {
                user = new ApplicationUser { UserName = loginName, IsStaff = true, CreatedAt = DateTime.Now };
                Check(await _users.CreateAsync(user));
            }
            user.FullName = entry.DisplayName ?? entry.SamAccountName;
            user.Email = entry.Mail;
            user.Patch = entry.Patch;
            user.LastSignInAt = DateTime.Now;
            Check(await _users.UpdateAsync(user));

            var existing = (await _users.GetClaimsAsync(user)).Where(c => c.Type == HousingClaimTypes.Role).ToList();
            if (existing.Count > 0) Check(await _users.RemoveClaimsAsync(user, existing));
            Check(await _users.AddClaimsAsync(user, roles.Select(r => new Claim(HousingClaimTypes.Role, r))));
            return user;
        }

        private static void Check(IdentityResult result)
        {
            if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
