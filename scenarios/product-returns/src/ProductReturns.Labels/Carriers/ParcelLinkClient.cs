using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductReturns.Labels.Services;

namespace ProductReturns.Labels.Carriers
{
    /// <summary>ParcelLink return label API v2.</summary>
    public class ParcelLinkClient : ICarrierClient
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient _http;
        private readonly CarrierOptions _options;
        private readonly ILogger<ParcelLinkClient> _logger;
        private readonly Lazy<string> _apiKey;

        public ParcelLinkClient(HttpClient http, IOptions<CarrierOptions> options, ILogger<ParcelLinkClient> logger)
        {
            _http = http;
            _options = options.Value;
            _logger = logger;
            _http.BaseAddress = new Uri(_options.BaseUrl);
            _http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
            _apiKey = new Lazy<string>(() => SecretProtector.Unprotect(_options.ApiKeyProtected));
        }

        public async Task<CarrierLabel> CreateReturnLabelAsync(LabelRequest request, CancellationToken cancellationToken)
        {
            var body = new
            {
                account = _options.AccountNumber,
                service = "RETURN_DROP_OFF",
                reference = request.Reference,
                sender = Map(request.From),
                recipient = Map(request.To),
                notification = new { email = request.CustomerEmail },
                parcels = new[] { new { weightGrams = (int)Math.Ceiling(Math.Max(0.1m, request.WeightKg) * 1000m) } },
                labelFormat = "PDF_A6"
            };

            using (var message = new HttpRequestMessage(HttpMethod.Post, "v2/returns/labels"))
            {
                message.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", _apiKey.Value);
                message.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");

                using (var response = await _http.SendAsync(message, cancellationToken))
                {
                    var text = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.UnprocessableEntity)
                    {
                        throw new CarrierException("Carrier rejected the label request: " + Truncate(text), permanent: true);
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new CarrierException("Carrier returned " + (int)response.StatusCode + ": " + Truncate(text));
                    }

                    var result = JsonSerializer.Deserialize<ParcelLinkResponse>(text, Json);
                    if (result?.Parcels == null || result.Parcels.Length == 0 || string.IsNullOrEmpty(result.LabelData))
                    {
                        throw new CarrierException("Carrier response did not contain a label.");
                    }

                    _logger.LogInformation("Label {Tracking} created for {Reference}", result.Parcels[0].TrackingNumber, request.Reference);
                    return new CarrierLabel { TrackingNumber = result.Parcels[0].TrackingNumber, Pdf = Convert.FromBase64String(result.LabelData) };
                }
            }
        }

        private static object Map(Address a) => new
        {
            name = a.Name,
            street = a.Street,
            postalCode = a.PostalCode,
            city = a.City,
            countryCode = a.Country
        };

        private static string Truncate(string text) => text == null ? string.Empty : text.Length > 300 ? text.Substring(0, 300) : text;

        private class ParcelLinkResponse
        {
            public string ShipmentId { get; set; }
            public ParcelLinkParcel[] Parcels { get; set; }
            public string LabelData { get; set; }
        }

        private class ParcelLinkParcel
        {
            public string TrackingNumber { get; set; }
        }
    }
}
