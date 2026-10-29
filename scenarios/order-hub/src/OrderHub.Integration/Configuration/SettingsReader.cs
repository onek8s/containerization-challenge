using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Litware.OrderHub.Integration.Configuration
{
    /// <summary>Typed access to flat key/value settings (appSettings in the services, a dictionary in tests).</summary>
    public class SettingsReader
    {
        private readonly Func<string, string> _lookup;

        public SettingsReader(Func<string, string> lookup)
        {
            _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        }

        public static SettingsReader FromDictionary(IDictionary<string, string> values)
        {
            return new SettingsReader(key => values.TryGetValue(key, out var v) ? v : null);
        }

        public string Get(string key, string defaultValue = null)
        {
            string value = _lookup(key);
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
        }

        public string Require(string key)
        {
            string value = Get(key);
            if (value == null) throw new InvalidOperationException("Setting '" + key + "' is missing.");
            return value;
        }

        public int GetInt(string key, int defaultValue)
        {
            string value = Get(key);
            return value == null ? defaultValue : int.Parse(value, CultureInfo.InvariantCulture);
        }

        public bool GetBool(string key, bool defaultValue)
        {
            string value = Get(key);
            return value == null ? defaultValue : bool.Parse(value);
        }

        public TimeSpan GetTime(string key, TimeSpan defaultValue)
        {
            string value = Get(key);
            return value == null ? defaultValue : TimeSpan.Parse(value, CultureInfo.InvariantCulture);
        }

        public IList<string> GetList(string key, string defaultValue)
        {
            return (Get(key, defaultValue) ?? "")
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }
    }
}
