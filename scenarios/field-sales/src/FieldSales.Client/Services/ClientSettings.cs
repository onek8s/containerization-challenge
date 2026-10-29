using System;
using System.Configuration;
using System.Globalization;

namespace Wingtip.FieldSales.Client.Services
{
    public class ClientSettings
    {
        public Uri ApiBaseUrl { get; private set; }
        public string ClientId { get; private set; }
        public TimeSpan SyncInterval { get; private set; }
        public TimeSpan RequestTimeout { get; private set; }

        public static ClientSettings Load()
        {
            var s = ConfigurationManager.AppSettings;
            var url = s["ApiBaseUrl"] ?? "http://localhost:50410/";
            if (!url.EndsWith("/")) url += "/";
            return new ClientSettings
            {
                ApiBaseUrl = new Uri(url),
                ClientId = s["ClientId"] ?? "fieldsales-desktop",
                SyncInterval = TimeSpan.FromMinutes(double.Parse(s["SyncIntervalMinutes"] ?? "10", CultureInfo.InvariantCulture)),
                RequestTimeout = TimeSpan.FromSeconds(double.Parse(s["RequestTimeoutSeconds"] ?? "30", CultureInfo.InvariantCulture))
            };
        }
    }
}
