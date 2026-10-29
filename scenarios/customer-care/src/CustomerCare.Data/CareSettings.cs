using System;
using System.Configuration;
using System.Globalization;

namespace Tailspin.CustomerCare.Data
{
    /// <summary>Application settings shared by the portal, the integration service and the tools (appSettings).</summary>
    public static class CareSettings
    {
        public static string TimeZoneId
        {
            get { return Get("CustomerCare:TimeZone", "FLE Standard Time"); }
        }

        public static TimeSpan OfficeOpens
        {
            get { return TimeSpan.Parse(Get("CustomerCare:OfficeOpens", "08:00"), CultureInfo.InvariantCulture); }
        }

        public static TimeSpan OfficeCloses
        {
            get { return TimeSpan.Parse(Get("CustomerCare:OfficeCloses", "17:00"), CultureInfo.InvariantCulture); }
        }

        public static string DefaultQueue
        {
            get { return Get("CustomerCare:DefaultQueue", "GENERAL"); }
        }

        public static string PortalUrl
        {
            get { return Get("CustomerCare:PortalUrl", "https://care.corp.local/"); }
        }

        public static string MailFrom
        {
            get { return Get("CustomerCare:MailFrom", "customer-care@tailspin-energy.example"); }
        }

        public static string ManagerEmail
        {
            get { return Get("CustomerCare:ManagerEmail", null); }
        }

        public static int MaxAttachmentBytes
        {
            get { return int.Parse(Get("CustomerCare:MaxAttachmentKB", "10240"), CultureInfo.InvariantCulture) * 1024; }
        }

        public static int ReferenceDataCacheMinutes
        {
            get { return int.Parse(Get("CustomerCare:ReferenceDataCacheMinutes", "60"), CultureInfo.InvariantCulture); }
        }

        public static TimeZoneInfo TimeZone
        {
            get { return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); }
        }

        public static string Get(string key, string defaultValue)
        {
            var v = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(v) ? defaultValue : v.Trim();
        }

        public static string Require(string key)
        {
            var v = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrWhiteSpace(v)) throw new ConfigurationErrorsException("appSettings key '" + key + "' is missing.");
            return v.Trim();
        }
    }
}
