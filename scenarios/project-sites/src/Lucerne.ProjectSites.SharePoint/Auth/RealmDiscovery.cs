using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace Lucerne.ProjectSites.SharePoint.Auth
{
    /// <summary>
    /// Finds the farm's realm (the SPAuthenticationRealm id) by sending an empty bearer token to client.svc and
    /// reading the WWW-Authenticate challenge. The value is cached per host.
    /// </summary>
    public class RealmDiscovery
    {
        private static readonly Regex RealmPattern = new Regex("realm=\"(?<realm>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private readonly ConcurrentDictionary<string, string> _cache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HttpMessageHandler _handler;

        public RealmDiscovery()
            : this(new HttpClientHandler { UseDefaultCredentials = false })
        {
        }

        public RealmDiscovery(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public string GetRealm(Uri siteUrl)
        {
            return _cache.GetOrAdd(siteUrl.Authority, _ => Discover(siteUrl));
        }

        public static string ParseRealm(string wwwAuthenticate)
        {
            if (string.IsNullOrEmpty(wwwAuthenticate))
            {
                return null;
            }

            var m = RealmPattern.Match(wwwAuthenticate);
            return m.Success ? m.Groups["realm"].Value : null;
        }

        private string Discover(Uri siteUrl)
        {
            using (var client = new HttpClient(_handler, false))
            {
                var request = new HttpRequestMessage(HttpMethod.Get, new Uri(siteUrl, "_vti_bin/client.svc"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", string.Empty);
                using (var response = client.SendAsync(request).GetAwaiter().GetResult())
                {
                    if (response.StatusCode != HttpStatusCode.Unauthorized)
                    {
                        throw new SharePointException("Expected 401 from " + request.RequestUri + ", got " + (int)response.StatusCode);
                    }

                    foreach (var header in response.Headers.WwwAuthenticate)
                    {
                        if (string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
                        {
                            var realm = ParseRealm(header.Parameter);
                            if (realm != null)
                            {
                                return realm;
                            }
                        }
                    }

                    throw new SharePointException("No bearer realm in the challenge from " + request.RequestUri);
                }
            }
        }
    }
}
