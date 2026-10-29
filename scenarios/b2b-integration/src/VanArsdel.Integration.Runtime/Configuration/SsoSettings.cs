using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.EnterpriseSingleSignOn.Interop;

namespace VanArsdel.Integration.Runtime.Configuration
{
    /// <summary>
    /// Settings of the SSO config application "VanArsdel.Integration" (identifier "ConfigProperties"): connection
    /// strings, archive share, SMTP relay, ERP service credentials. Read with SSO_FLAG_RUNTIME by the host
    /// account and cached per host instance; the deploy scripts write them with the SSO application tool.
    /// </summary>
    public static class SsoSettings
    {
        public const string ApplicationName = "VanArsdel.Integration";
        public const string Identifier = "ConfigProperties";

        private static readonly TimeSpan CacheTime = TimeSpan.FromMinutes(10);
        private static readonly object Gate = new object();
        private static IDictionary<string, string> _cached;
        private static DateTime _loadedUtc;

        /// <summary>Replaces the SSO lookup (unit tests, tools that run outside a BizTalk host).</summary>
        public static Func<IDictionary<string, string>> Source { get; set; }

        public static string Get(string name)
        {
            string value;
            if (!Values.TryGetValue(name, out value) || string.IsNullOrEmpty(value))
            {
                throw new KeyNotFoundException("SSO application " + ApplicationName + " has no value for '" + name + "'.");
            }

            return value;
        }

        public static string Get(string name, string defaultValue)
        {
            string value;
            return Values.TryGetValue(name, out value) && !string.IsNullOrEmpty(value) ? value : defaultValue;
        }

        public static int GetInt(string name, int defaultValue)
        {
            int value;
            return int.TryParse(Get(name, null), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : defaultValue;
        }

        public static bool GetBool(string name, bool defaultValue)
        {
            bool value;
            return bool.TryParse(Get(name, null), out value) ? value : defaultValue;
        }

        public static void Refresh()
        {
            lock (Gate)
            {
                _cached = null;
            }
        }

        private static IDictionary<string, string> Values
        {
            get
            {
                lock (Gate)
                {
                    if (_cached == null || DateTime.UtcNow - _loadedUtc > CacheTime)
                    {
                        _cached = Source != null ? Source() : ReadStore();
                        _loadedUtc = DateTime.UtcNow;
                    }

                    return _cached;
                }
            }
        }

        private static IDictionary<string, string> ReadStore()
        {
            var bag = new SsoPropertyBag();
            ((ISSOConfigStore)new SSOConfigStore()).GetConfigInfo(ApplicationName, Identifier, SSOFlag.SSO_FLAG_RUNTIME, bag);
            return bag.Values.ToDictionary(v => v.Key, v => Convert.ToString(v.Value, CultureInfo.InvariantCulture), StringComparer.OrdinalIgnoreCase);
        }
    }
}
