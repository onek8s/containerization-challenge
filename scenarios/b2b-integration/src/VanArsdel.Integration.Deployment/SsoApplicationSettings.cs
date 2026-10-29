using System;
using System.Collections.Generic;
using System.Linq;

namespace VanArsdel.Integration.Deployment
{
    /// <summary>
    /// The settings that go to the SSO configuration store application "VanArsdel.Integration" (one identifier,
    /// "ConfigProperties"). Settings whose name starts with "SSO_" in the workbook are exported there (prefix
    /// removed); secrets are not in the workbook but entered at deploy time from the password vault.
    /// </summary>
    public class SsoApplicationSettings
    {
        public const string ApplicationName = "VanArsdel.Integration";
        public const string Identifier = "ConfigProperties";
        public const string Prefix = "SSO_";

        public SsoApplicationSettings(IDictionary<string, string> values)
        {
            Values = new SortedDictionary<string, string>(values, StringComparer.Ordinal);
        }

        public IDictionary<string, string> Values { get; private set; }

        public static SsoApplicationSettings FromEnvironment(EnvironmentSettings env, IDictionary<string, string> secrets)
        {
            var values = env.Values.Where(v => v.Key.StartsWith(Prefix, StringComparison.Ordinal))
                .ToDictionary(v => v.Key.Substring(Prefix.Length), v => v.Value, StringComparer.Ordinal);
            if (secrets != null)
            {
                foreach (var s in secrets)
                {
                    values[s.Key] = s.Value;
                }
            }

            return new SsoApplicationSettings(values);
        }

        public IEnumerable<string> MissingRequired(IEnumerable<string> required)
        {
            return required.Where(r => !Values.ContainsKey(r) || string.IsNullOrEmpty(Values[r]));
        }
    }
}
