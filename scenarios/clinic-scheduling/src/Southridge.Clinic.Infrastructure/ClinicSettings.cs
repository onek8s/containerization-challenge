using System;
using System.Configuration;
using System.Globalization;

namespace Southridge.Clinic.Infrastructure
{
    /// <summary>Typed access to appSettings. Keys are prefixed with "Clinic:".</summary>
    public static class ClinicSettings
    {
        public static string Get(string key)
        {
            return ConfigurationManager.AppSettings["Clinic:" + key];
        }

        public static string Get(string key, string defaultValue)
        {
            string value = Get(key);
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        public static string Required(string key)
        {
            string value = Get(key);
            if (string.IsNullOrEmpty(value))
            {
                throw new ConfigurationErrorsException("The appSetting Clinic:" + key + " is missing.");
            }

            return value;
        }

        public static int GetInt(string key, int defaultValue)
        {
            int value;
            return int.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : defaultValue;
        }

        public static bool GetBool(string key, bool defaultValue)
        {
            bool value;
            return bool.TryParse(Get(key), out value) ? value : defaultValue;
        }

        public static decimal GetDecimal(string key, decimal defaultValue)
        {
            decimal value;
            return decimal.TryParse(Get(key), NumberStyles.Number, CultureInfo.InvariantCulture, out value) ? value : defaultValue;
        }

        public static TimeSpan GetTimeSpan(string key, TimeSpan defaultValue)
        {
            TimeSpan value;
            return TimeSpan.TryParse(Get(key), CultureInfo.InvariantCulture, out value) ? value : defaultValue;
        }

        public static string ConnectionString(string name)
        {
            ConnectionStringSettings cs = ConfigurationManager.ConnectionStrings[name];
            if (cs == null)
            {
                throw new ConfigurationErrorsException("The connection string " + name + " is missing.");
            }

            return cs.ConnectionString;
        }
    }
}
