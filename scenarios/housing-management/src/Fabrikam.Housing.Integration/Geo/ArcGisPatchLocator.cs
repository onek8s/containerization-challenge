using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Fabrikam.Housing.Integration.Geo
{
    /// <summary>
    /// Point-in-polygon query against the housing patches layer on the internal ArcGIS Server.
    /// The GIS server uses Windows authentication, so the call runs as the application pool identity.
    /// </summary>
    public class ArcGisPatchLocator : IPatchLocator
    {
        private readonly IRestClient _client;
        private readonly ILogger<ArcGisPatchLocator> _logger;

        public ArcGisPatchLocator(RestClientFactory factory, IOptions<IntegrationOptions> options, ILogger<ArcGisPatchLocator> logger)
        {
            _logger = logger;
            _client = factory.Create(options.Value.Gis.PatchLayerUrl, internalHost: true);
        }

        public async Task<string> FindPatchAsync(double latitude, double longitude)
        {
            var request = new RestRequest("query", Method.GET) { UseDefaultCredentials = true };
            request.AddQueryParameter("geometry", longitude.ToString(CultureInfo.InvariantCulture) + "," + latitude.ToString(CultureInfo.InvariantCulture));
            request.AddQueryParameter("geometryType", "esriGeometryPoint");
            request.AddQueryParameter("inSR", "4326");
            request.AddQueryParameter("spatialRel", "esriSpatialRelIntersects");
            request.AddQueryParameter("outFields", "PATCH_CODE");
            request.AddQueryParameter("returnGeometry", "false");
            request.AddQueryParameter("f", "json");

            var response = await RestClientFactory.RetryPolicy(_logger, "GIS").ExecuteAsync(() => _client.ExecuteAsync(request));
            RestClientFactory.EnsureSuccess(response, "GIS");
            var json = JObject.Parse(response.Content);
            if (json["error"] != null)
                throw new IntegrationException("GIS", (int?)json["error"]["code"] ?? 0, (string)json["error"]["message"]);
            var features = json["features"] as JArray;
            return features == null || features.Count == 0 ? null : (string)features[0]["attributes"]["PATCH_CODE"];
        }
    }
}
