using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;

namespace Tailspin.CustomerCare.Integration
{
    /// <summary>Tells the portal (care.corp.local) that a case changed so that agents see it immediately.</summary>
    public static class PortalNotifier
    {
        public static void Notify(int caseId, string change)
        {
            var url = ConfigurationManager.AppSettings["CustomerCare:PortalNotifyUrl"];
            var key = ConfigurationManager.AppSettings["CustomerCare:NotifyKey"];
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key)) return;

            var target = url + "?caseId=" + caseId.ToString(CultureInfo.InvariantCulture) + "&change=" + Uri.EscapeDataString(change ?? "updated");
            Task.Run(() =>
            {
                try
                {
                    using (var client = new WebClient())
                    {
                        client.Headers["X-Care-Key"] = key;
                        client.UploadString(target, "POST", string.Empty);
                    }
                }
                catch (WebException ex)
                {
                    // The portal picks the change up on the next refresh anyway.
                    Trace.TraceWarning("Portal notification for case {0} failed: {1}", caseId, ex.Message);
                }
            });
        }
    }
}
