using System;
using System.Globalization;
using VanArsdel.Integration.Domain.Codes;

namespace VanArsdel.Integration.Mapping.Functoids
{
    /// <summary>Scripting functoid class (external assembly): date conversions between EDI and xs:date.</summary>
    public class DateFunctoids
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Clock used by Today(); the lab runner sets it to the simulated business date.</summary>
        public static Func<DateTime> Clock { get; set; } = () => DateTime.Now;

        public string X12ToXmlDate(string value)
        {
            var d = EdiDate.ParseX12Date(value);
            return d.HasValue ? EdiDate.ToXmlDate(d.Value) : string.Empty;
        }

        public string EdifactToXmlDate(string value, string formatQualifier)
        {
            var d = EdiDate.ParseEdifact(value, formatQualifier);
            return d.HasValue ? EdiDate.ToXmlDate(d.Value) : string.Empty;
        }

        public string XmlDateToX12(string value)
        {
            DateTime d;
            return TryXml(value, out d) ? EdiDate.ToX12Date(d) : string.Empty;
        }

        public string XmlDateToEdifact(string value)
        {
            DateTime d;
            return TryXml(value, out d) ? EdiDate.ToEdifact(d, "102") : string.Empty;
        }

        /// <summary>yyyy-MM-dd to SAP IDoc date (yyyyMMdd).</summary>
        public string XmlDateToIDoc(string value)
        {
            return XmlDateToX12(value);
        }

        public string IDocToXmlDate(string value)
        {
            return X12ToXmlDate(value);
        }

        public string Today()
        {
            return EdiDate.ToXmlDate(Clock());
        }

        public string TodayX12()
        {
            return EdiDate.ToX12Date(Clock());
        }

        public string NowTimeX12()
        {
            return EdiDate.ToX12Time(Clock());
        }

        /// <summary>Current time as an xs:dateTime in UTC (ack receipt stamps).</summary>
        public string NowUtc()
        {
            return Clock().ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", Inv);
        }

        public string AddDays(string xmlDate, double days)
        {
            DateTime d;
            return TryXml(xmlDate, out d) ? EdiDate.ToXmlDate(d.AddDays(days)) : string.Empty;
        }

        /// <summary>Due date from invoice date and payment terms in days.</summary>
        public string DueDate(string invoiceXmlDate, string termsDays)
        {
            int days;
            if (!int.TryParse(termsDays, NumberStyles.Integer, Inv, out days))
            {
                days = 30;
            }

            return AddDays(invoiceXmlDate, days);
        }

        private static bool TryXml(string value, out DateTime d)
        {
            return DateTime.TryParseExact((value ?? string.Empty).Trim(), "yyyy-MM-dd", Inv, DateTimeStyles.None, out d);
        }
    }
}
