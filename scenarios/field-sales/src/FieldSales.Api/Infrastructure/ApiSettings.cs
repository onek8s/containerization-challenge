using System;
using System.Configuration;
using System.Globalization;

namespace Wingtip.FieldSales.Api.Infrastructure
{
    public class ApiSettings
    {
        public string MinimumClientVersion { get; set; }
        public TimeSpan OrderCutOff { get; set; }
        public TimeSpan AccessTokenLifetime { get; set; }
        public TimeSpan RefreshTokenLifetime { get; set; }
        public bool AllowInsecureHttp { get; set; }
        public string DesktopClientId { get; set; }

        public static ApiSettings FromConfig()
        {
            var s = ConfigurationManager.AppSettings;
            return new ApiSettings
            {
                MinimumClientVersion = s["FieldSales:MinimumClientVersion"],
                OrderCutOff = TimeSpan.Parse(s["FieldSales:OrderCutOff"] ?? "14:00", CultureInfo.InvariantCulture),
                AccessTokenLifetime = TimeSpan.FromHours(double.Parse(s["FieldSales:AccessTokenHours"] ?? "8", CultureInfo.InvariantCulture)),
                RefreshTokenLifetime = TimeSpan.FromDays(double.Parse(s["FieldSales:RefreshTokenDays"] ?? "14", CultureInfo.InvariantCulture)),
                AllowInsecureHttp = string.Equals(s["FieldSales:AllowInsecureHttp"], "true", StringComparison.OrdinalIgnoreCase),
                DesktopClientId = s["FieldSales:DesktopClientId"] ?? "fieldsales-desktop"
            };
        }
    }
}
