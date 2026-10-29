using System;
using System.Configuration;
using System.Globalization;
using System.Linq;
using Tailspin.DealerOrders.Processing;

namespace Tailspin.DealerOrders.Infrastructure
{
    public static class Settings
    {
        public static string Get(string key, string defaultValue = null)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        public static string Required(string key) =>
            ConfigurationManager.AppSettings[key] ?? throw new ConfigurationErrorsException($"App setting '{key}' is missing.");

        public static int Int(string key, int defaultValue) =>
            int.TryParse(ConfigurationManager.AppSettings[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : defaultValue;

        public static bool Bool(string key, bool defaultValue) =>
            bool.TryParse(ConfigurationManager.AppSettings[key], out var v) ? v : defaultValue;

        public static TimeSpan Time(string key, TimeSpan defaultValue) =>
            TimeSpan.TryParse(ConfigurationManager.AppSettings[key], CultureInfo.InvariantCulture, out var v) ? v : defaultValue;

        public static ProcessingSettings Processing()
        {
            var s = new ProcessingSettings();
            s.AxCompany = Get("Ax.Company", s.AxCompany);
            s.AxLanguage = Get("Ax.Language", s.AxLanguage);
            s.QuoteValidityDays = Int("Orders.QuoteValidityDays", s.QuoteValidityDays);
            s.AxMaxAttempts = Int("Ax.MaxAttempts", s.AxMaxAttempts);
            s.SalesDeskEmail = Get("Orders.SalesDeskEmail", s.SalesDeskEmail);
            s.PortalBaseUrl = Get("Orders.PortalBaseUrl", s.PortalBaseUrl);
            s.SalesDeskBaseUrl = Get("Orders.SalesDeskBaseUrl", s.SalesDeskBaseUrl);
            s.OurInterchangeQualifier = Get("Edi.InterchangeQualifier", s.OurInterchangeQualifier);
            s.OurInterchangeId = Get("Edi.InterchangeId", s.OurInterchangeId);
            s.OurApplicationCode = Get("Edi.ApplicationCode", s.OurApplicationCode);
            s.EdiTestIndicator = Bool("Edi.Test", s.EdiTestIndicator);
            var retry = Get("Ax.RetryDelays");
            if (!string.IsNullOrEmpty(retry))
                s.AxRetryDelays = retry.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(t => TimeSpan.Parse(t.Trim(), CultureInfo.InvariantCulture)).ToArray();
            return s;
        }
    }
}
