using System;
using System.Net;
using System.Threading.Tasks;
using Fabrikam.Housing.Common;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Fabrikam.Housing.Integration.Geo
{
    public class PostcodesIoLookup : IPostcodeLookup
    {
        private readonly IRestClient _client;
        private readonly IMemoryCache _cache;
        private readonly PostcodeOptions _options;
        private readonly ILogger<PostcodesIoLookup> _logger;

        public PostcodesIoLookup(RestClientFactory factory, IMemoryCache cache, IOptions<IntegrationOptions> options, ILogger<PostcodesIoLookup> logger)
        {
            _options = options.Value.Postcodes;
            _cache = cache;
            _logger = logger;
            _client = factory.Create(_options.BaseUrl);
        }

        public async Task<PostcodeResult> LookupAsync(string postcode)
        {
            if (!Postcode.TryNormalise(postcode, out var normalised)) return null;
            if (_cache.TryGetValue("postcode:" + normalised, out PostcodeResult cached)) return cached;

            var request = new RestRequest("postcodes/{postcode}", Method.GET);
            request.AddUrlSegment("postcode", normalised.Replace(" ", ""));
            var response = await RestClientFactory.RetryPolicy(_logger, "Postcodes").ExecuteAsync(() => _client.ExecuteAsync(request));
            PostcodeResult result = null;
            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                RestClientFactory.EnsureSuccess(response, "Postcodes");
                var r = JObject.Parse(response.Content)["result"];
                result = new PostcodeResult
                {
                    Postcode = (string)r["postcode"],
                    Latitude = (double)r["latitude"],
                    Longitude = (double)r["longitude"],
                    AdminDistrict = (string)r["admin_district"],
                    Ward = (string)r["admin_ward"]
                };
            }
            _cache.Set("postcode:" + normalised, result, TimeSpan.FromHours(_options.CacheHours));
            return result;
        }
    }
}
