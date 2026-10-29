using System;
using System.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Tailspin.CustomerCare.Data.Repositories;
using Tailspin.CustomerCare.Web.Realtime;

namespace Tailspin.CustomerCare.Web.Handlers
{
    /// <summary>
    /// POST /internal/notify.ashx?caseId=123&amp;change=created
    /// Called by the integration service after it created or changed a case, so that connected agents see it.
    /// Authenticated with the shared key in the X-Care-Key header.
    /// </summary>
    public class NotifyHandler : IHttpHandler
    {
        public bool IsReusable
        {
            get { return true; }
        }

        public void ProcessRequest(HttpContext context)
        {
            if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 405;
                return;
            }

            var expected = ConfigurationManager.AppSettings["CustomerCare:NotifyKey"];
            var actual = context.Request.Headers["X-Care-Key"];
            if (string.IsNullOrEmpty(expected) || !FixedTimeEquals(expected, actual))
            {
                context.Response.StatusCode = 403;
                return;
            }

            int caseId;
            if (!int.TryParse(context.Request["caseId"], out caseId))
            {
                context.Response.StatusCode = 400;
                return;
            }

            var c = new CaseRepository().Get(caseId);
            if (c == null)
            {
                context.Response.StatusCode = 404;
                return;
            }

            QueueNotifier.Publish(c, context.Request["change"] ?? "updated");
            context.Response.StatusCode = 204;
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            if (b == null) return false;
            var x = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(a));
            var y = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(b));
            var diff = 0;
            for (var i = 0; i < x.Length; i++) diff |= x[i] ^ y[i];
            return diff == 0;
        }
    }
}
