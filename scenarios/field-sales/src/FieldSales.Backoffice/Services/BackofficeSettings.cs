using System.Configuration;
using System.Globalization;

namespace Wingtip.FieldSales.Backoffice.Services
{
    public class BackofficeSettings
    {
        public CultureInfo PriceListCulture { get; set; }
        public string ClientInstallUrl { get; set; }

        public static BackofficeSettings FromConfig()
        {
            var s = ConfigurationManager.AppSettings;
            return new BackofficeSettings
            {
                PriceListCulture = CultureInfo.GetCultureInfo(s["FieldSales:PriceListCulture"] ?? "fi-FI"),
                ClientInstallUrl = s["FieldSales:ClientInstallUrl"]
            };
        }
    }
}
