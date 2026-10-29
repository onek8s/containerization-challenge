using System;
using System.Collections.Generic;

namespace Tailspin.DealerOrders.Processing
{
    /// <summary>Business settings shared by the web applications and services (appSettings "Orders.*" and "Ax.*").</summary>
    public class ProcessingSettings
    {
        public string AxCompany { get; set; } = "TSM";
        public string AxLanguage { get; set; } = "en-us";
        public int QuoteValidityDays { get; set; } = 30;
        public decimal PriceTolerancePercent { get; set; } = 0.5m;
        public int AxMaxAttempts { get; set; } = 12;

        /// <summary>Delay before each retry of a command that failed because AX was not reachable.</summary>
        public TimeSpan[] AxRetryDelays { get; set; } =
        {
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30), TimeSpan.FromHours(1)
        };

        public string SalesDeskEmail { get; set; } = "salesdesk@tailspin-machinery.example";
        public string PortalBaseUrl { get; set; } = "https://dealers.corp.local/";
        public string SalesDeskBaseUrl { get; set; } = "https://salesdesk.corp.local/";

        /// <summary>Countries where export-controlled models ship without a separate check (EU and EFTA).</summary>
        public HashSet<string> FreeTradeCountries { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "ES", "FI", "FR", "GR", "HR", "HU", "IE", "IT", "LT", "LU", "LV", "MT",
            "NL", "PL", "PT", "RO", "SE", "SI", "SK", "GB", "NO", "IS", "CH", "LI"
        };

        public string OurInterchangeQualifier { get; set; } = "ZZ";
        public string OurInterchangeId { get; set; } = "TAILSPINMACH";
        public string OurApplicationCode { get; set; } = "TAILSPIN";
        public bool EdiTestIndicator { get; set; }
        public string ShipFromName { get; set; } = "Tailspin Machinery Oy, Kontiolahti";
    }
}
