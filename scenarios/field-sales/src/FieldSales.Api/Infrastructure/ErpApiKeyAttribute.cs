using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using log4net;

namespace Wingtip.FieldSales.Api.Infrastructure
{
    /// <summary>
    /// The ERP integration jobs call the API with a shared key (FieldSales:ErpApiKey) in the X-Api-Key header
    /// instead of a user token.
    /// </summary>
    public class ErpApiKeyAttribute : AuthorizationFilterAttribute
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ErpApiKeyAttribute));

        public override void OnAuthorization(HttpActionContext actionContext)
        {
            var expected = ConfigurationManager.AppSettings["FieldSales:ErpApiKey"];
            var request = actionContext.Request;
            var given = request.Headers.Contains("X-Api-Key") ? request.Headers.GetValues("X-Api-Key").FirstOrDefault() : null;

            if (string.IsNullOrEmpty(expected) || given == null || !FixedTimeEquals(expected, given))
            {
                Log.WarnFormat("ERP call {0} refused from {1}", request.RequestUri.PathAndQuery, ClientAddress(request));
                actionContext.Response = request.CreateErrorResponse(HttpStatusCode.Unauthorized, "Invalid API key.");
            }
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            var x = Encoding.UTF8.GetBytes(a);
            var y = Encoding.UTF8.GetBytes(b);
            using (var sha = SHA256.Create())
            {
                x = sha.ComputeHash(x);
                y = sha.ComputeHash(y);
            }
            var diff = 0;
            for (var i = 0; i < x.Length; i++) diff |= x[i] ^ y[i];
            return diff == 0;
        }

        private static string ClientAddress(HttpRequestMessage request)
        {
            object context;
            if (request.Properties.TryGetValue("MS_HttpContext", out context))
            {
                var http = context as System.Web.HttpContextWrapper;
                if (http != null) return http.Request.UserHostAddress;
            }
            return "?";
        }
    }
}
