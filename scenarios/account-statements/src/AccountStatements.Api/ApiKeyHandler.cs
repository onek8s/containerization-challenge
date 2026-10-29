using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Woodgrove.Statements.Core.Model;
using Woodgrove.Statements.Core.Security;
using Woodgrove.Statements.Data;

namespace Woodgrove.Statements.Api
{
    /// <summary>
    /// Every request except /api/health needs X-Api-Key. Keys are issued in the portal (Admin, API clients); clients are
    /// cached for five minutes so a disabled key stops working within that time.
    /// </summary>
    public class ApiKeyHandler : DelegatingHandler
    {
        public const string ClientProperty = "Statements.ApiClient";
        static readonly Dictionary<string, Tuple<ApiClient, DateTime>> Cache = new Dictionary<string, Tuple<ApiClient, DateTime>>();
        readonly ApiClientRepository _clients;

        public ApiKeyHandler(ApiClientRepository clients)
        {
            _clients = clients;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri.AbsolutePath.EndsWith("/api/health", StringComparison.OrdinalIgnoreCase))
                return await base.SendAsync(request, cancellationToken);

            IEnumerable<string> values;
            var key = request.Headers.TryGetValues("X-Api-Key", out values) ? values.FirstOrDefault() : null;
            var client = Find(key);
            if (client == null)
            {
                var response = request.CreateErrorResponse(HttpStatusCode.Unauthorized, "A valid X-Api-Key header is required.");
                response.Headers.Add("WWW-Authenticate", "ApiKey");
                return response;
            }

            request.Properties[ClientProperty] = client;
            var principal = new GenericPrincipal(new GenericIdentity("api:" + client.Name, "ApiKey"), new string[0]);
            Thread.CurrentPrincipal = principal;
            if (HttpContext.Current != null) HttpContext.Current.User = principal;
            if (!client.LastUsedAt.HasValue || client.LastUsedAt.Value < DateTime.Now.AddMinutes(-10))
            {
                client.LastUsedAt = DateTime.Now;
                _clients.Touch(client.ClientId);
            }
            return await base.SendAsync(request, cancellationToken);
        }

        ApiClient Find(string key)
        {
            var prefix = ApiKey.Prefix(key);
            if (prefix == null) return null;
            var hash = ApiKey.Hash(key);
            Tuple<ApiClient, DateTime> cached;
            lock (Cache)
            {
                if (Cache.TryGetValue(prefix, out cached) && cached.Item2 > DateTime.UtcNow)
                    return cached.Item1 != null && cached.Item1.Enabled && ApiKey.FixedTimeEquals(cached.Item1.KeyHash, hash) ? cached.Item1 : null;
            }
            var client = _clients.FindByPrefix(prefix);
            lock (Cache)
                Cache[prefix] = Tuple.Create(client, DateTime.UtcNow.AddMinutes(5));
            return client != null && client.Enabled && ApiKey.FixedTimeEquals(client.KeyHash, hash) ? client : null;
        }
    }
}
