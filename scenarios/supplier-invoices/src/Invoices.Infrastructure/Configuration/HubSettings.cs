using System;
using System.Configuration;
using System.Globalization;

namespace Wingtip.Invoices.Infrastructure.Configuration
{
    /// <summary>Typed access to the appSettings of the hub services.</summary>
    public static class HubSettings
    {
        public static string ConnectionString
        {
            get
            {
                var cs = ConfigurationManager.ConnectionStrings["Invoices"];
                if (cs == null) throw new ConfigurationErrorsException("Connection string 'Invoices' is missing.");
                return cs.ConnectionString;
            }
        }

        public static string Get(string key, string defaultValue = null)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
        }

        public static string Require(string key)
        {
            var value = Get(key);
            if (value == null) throw new ConfigurationErrorsException("appSettings '" + key + "' is missing.");
            return value;
        }

        public static int GetInt(string key, int defaultValue)
        {
            int result;
            return int.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? result : defaultValue;
        }

        public static decimal GetDecimal(string key, decimal defaultValue)
        {
            decimal result;
            return decimal.TryParse(Get(key), NumberStyles.Number, CultureInfo.InvariantCulture, out result) ? result : defaultValue;
        }

        public static bool GetBool(string key, bool defaultValue)
        {
            bool result;
            return bool.TryParse(Get(key), out result) ? result : defaultValue;
        }

        public static TimeSpan GetSeconds(string key, int defaultSeconds)
        {
            return TimeSpan.FromSeconds(GetInt(key, defaultSeconds));
        }

        /// <summary>Company code in the ERP (WT01 = Wingtip Supply Oy).</summary>
        public static string Company
        {
            get { return Get("Company", "WT01"); }
        }

        /// <summary>Root of the archive share, e.g. \\files01.corp.local\InvoiceArchive$.</summary>
        public static string ArchiveRoot
        {
            get { return Require("ArchiveRoot"); }
        }

        /// <summary>Local working folder, e.g. D:\InvoiceHub\Work.</summary>
        public static string WorkFolder
        {
            get { return Get("WorkFolder", @"D:\InvoiceHub\Work"); }
        }
    }
}
