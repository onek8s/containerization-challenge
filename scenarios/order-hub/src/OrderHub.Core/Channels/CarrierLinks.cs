using System;
using System.Collections.Generic;

namespace Litware.OrderHub.Core.Channels
{
    /// <summary>Public tracking pages of the carriers the warehouse uses.</summary>
    public static class CarrierLinks
    {
        private static readonly Dictionary<string, string> Templates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "POSTI", "https://www.posti.fi/en/tracking#/lahetys/{0}" },
            { "POSTNORD", "https://www.postnord.se/en/our-tools/track-and-trace?shipmentId={0}" },
            { "DHL", "https://www.dhl.com/fi-en/home/tracking/tracking-parcel.html?tracking-id={0}" },
            { "DBSCHENKER", "https://www.dbschenker.com/app/tracking-public/?refNumber={0}" },
            { "MATKAHUOLTO", "https://www.matkahuolto.fi/seuranta?parcelNumber={0}" }
        };

        public static string TrackingUrl(string carrier, string trackingNumber)
        {
            if (string.IsNullOrEmpty(carrier) || string.IsNullOrEmpty(trackingNumber)) return null;
            string template;
            return Templates.TryGetValue(carrier.Replace(" ", ""), out template)
                ? string.Format(template, Uri.EscapeDataString(trackingNumber))
                : null;
        }
    }
}
