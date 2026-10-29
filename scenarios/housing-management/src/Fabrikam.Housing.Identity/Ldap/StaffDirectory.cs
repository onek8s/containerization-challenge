using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Novell.Directory.Ldap;

namespace Fabrikam.Housing.Identity.Ldap
{
    public interface IStaffDirectory
    {
        /// <summary>Validates the password and returns the account, or null when the credentials are wrong.</summary>
        DirectoryUser Authenticate(string samAccountName, string password);
    }

    public static class StaffAccountName
    {
        /// <summary>Accepts CORP\name, name@corp.local; returns the sAMAccountName or null for non-staff names.</summary>
        public static string Parse(string userName, LdapOptions options)
        {
            if (string.IsNullOrWhiteSpace(userName)) return null;
            var value = userName.Trim();
            var prefix = options.Domain + "\\";
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return value.Substring(prefix.Length);
            var suffix = "@" + options.DnsDomain;
            if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return value.Substring(0, value.Length - suffix.Length);
            return null;
        }
    }

    /// <summary>
    /// Binds to Active Directory over LDAPS as the user (UPN bind) and reads the account and group memberships.
    /// </summary>
    public class LdapStaffDirectory : IStaffDirectory
    {
        private readonly LdapOptions _options;
        private readonly ILogger<LdapStaffDirectory> _logger;

        public LdapStaffDirectory(IOptions<LdapOptions> options, ILogger<LdapStaffDirectory> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public DirectoryUser Authenticate(string samAccountName, string password)
        {
            if (string.IsNullOrEmpty(password)) return null;
            using (var connection = new LdapConnection { SecureSocketLayer = _options.UseSsl })
            {
                try
                {
                    connection.Connect(_options.Server, _options.Port);
                    connection.Bind(samAccountName + "@" + _options.DnsDomain, password);
                }
                catch (LdapException ex) when (ex.ResultCode == LdapException.InvalidCredentials)
                {
                    _logger.LogWarning("LDAP bind failed for {Account}", samAccountName);
                    return null;
                }

                var results = connection.Search(_options.SearchBase, LdapConnection.ScopeSub,
                    $"(&(objectClass=user)(sAMAccountName={Escape(samAccountName)}))",
                    new[] { "sAMAccountName", "displayName", "mail", "memberOf", "extensionAttribute1" }, false);

                while (results.HasMore())
                {
                    LdapEntry entry;
                    try { entry = results.Next(); }
                    catch (LdapReferralException) { continue; }

                    var attributes = entry.GetAttributeSet();
                    return new DirectoryUser
                    {
                        SamAccountName = Value(attributes, "sAMAccountName") ?? samAccountName,
                        DisplayName = Value(attributes, "displayName"),
                        Mail = Value(attributes, "mail"),
                        Patch = Value(attributes, "extensionAttribute1"),
                        Groups = attributes.ContainsKey("memberOf")
                            ? attributes.GetAttribute("memberOf").StringValueArray.Select(CommonName).ToList()
                            : new List<string>()
                    };
                }
                return null;
            }
        }

        private static string Value(LdapAttributeSet set, string name) =>
            set.ContainsKey(name) ? set.GetAttribute(name).StringValue : null;

        private static string CommonName(string dn)
        {
            var first = dn.Split(',')[0];
            return first.StartsWith("CN=", StringComparison.OrdinalIgnoreCase) ? first.Substring(3) : first;
        }

        private static string Escape(string value) =>
            value.Replace("\\", "\\5c").Replace("*", "\\2a").Replace("(", "\\28").Replace(")", "\\29");
    }

    /// <summary>Development directory with accounts from configuration (Ldap:SimulatedUsers).</summary>
    public class SimulatedStaffDirectory : IStaffDirectory
    {
        private readonly LdapOptions _options;

        public SimulatedStaffDirectory(IOptions<LdapOptions> options)
        {
            _options = options.Value;
        }

        public DirectoryUser Authenticate(string samAccountName, string password)
        {
            var user = _options.SimulatedUsers.FirstOrDefault(u => string.Equals(u.SamAccountName, samAccountName, StringComparison.OrdinalIgnoreCase));
            if (user == null || user.Password != password) return null;
            return new DirectoryUser { SamAccountName = user.SamAccountName, DisplayName = user.DisplayName, Mail = user.Mail, Patch = user.Patch, Groups = user.Groups };
        }
    }
}
