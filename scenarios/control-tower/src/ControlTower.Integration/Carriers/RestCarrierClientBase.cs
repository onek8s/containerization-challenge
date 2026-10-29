using System.Net;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Polly;
using Polly.Retry;
using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Domain.Parties;
using Proseware.ControlTower.Domain.Shipments;
using RestSharp;

namespace Proseware.ControlTower.Integration.Carriers;

public abstract class RestCarrierClientBase : ICarrierApiClient
{
    protected static readonly JsonSerializerSettings Json = new()
    {
        ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
        NullValueHandling = NullValueHandling.Ignore,
        DateParseHandling = DateParseHandling.DateTimeOffset
    };

    private readonly CarrierIntegrationOptions _options;
    private readonly AsyncRetryPolicy<IRestResponse> _retry;

    protected RestCarrierClientBase(CarrierIntegrationOptions options, ILogger logger)
    {
        _options = options;
        Logger = logger;
        _retry = Policy
            .HandleResult<IRestResponse>(r => r.ResponseStatus != ResponseStatus.Completed
                                              || r.StatusCode == HttpStatusCode.TooManyRequests
                                              || (int)r.StatusCode >= 500)
            .WaitAndRetryAsync(4, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                (outcome, delay, attempt, _) => Logger.LogWarning("Carrier API call failed with {Status} {Error}, retry {Attempt} in {Delay}",
                    outcome.Result?.StatusCode, outcome.Result?.ErrorMessage, attempt, delay));
    }

    protected ILogger Logger { get; }

    public abstract IReadOnlyCollection<string> CarrierCodes { get; }

    public abstract Task<CarrierBookingResult> BookAsync(Carrier carrier, Shipment shipment, Leg leg, CancellationToken cancellationToken);

    public abstract Task<IReadOnlyList<StatusReported>> GetTrackingAsync(Carrier carrier, IReadOnlyList<TrackingQuery> queries, DateTimeOffset since, CancellationToken cancellationToken);

    protected CarrierApiOptions Settings(Carrier carrier) => _options.ApiFor(carrier.Code);

    protected RestClient CreateClient(Carrier carrier)
    {
        var settings = Settings(carrier);
        var baseUrl = settings.BaseUrl ?? carrier.ApiBaseUrl ?? throw new InvalidOperationException($"No API URL for carrier {carrier.Code}.");
        return new RestClient(baseUrl)
        {
            Timeout = settings.TimeoutSeconds * 1000,
            UserAgent = "Proseware-ControlTower/4"
        };
    }

    protected async Task<IRestResponse> ExecuteAsync(RestClient client, IRestRequest request, CancellationToken ct)
    {
        var response = await _retry.ExecuteAsync(token => client.ExecuteAsync(request, token), ct);
        Logger.LogDebug("{Method} {Resource} -> {Status}", request.Method, request.Resource, (int)response.StatusCode);
        return response;
    }

    protected static T? Deserialize<T>(IRestResponse response) =>
        string.IsNullOrEmpty(response.Content) ? default : JsonConvert.DeserializeObject<T>(response.Content, Json);

    protected static RestRequest JsonRequest(string resource, Method method, object? body = null)
    {
        var request = new RestRequest(resource, method);
        request.AddHeader("Accept", "application/json");
        if (body is not null)
            request.AddParameter("application/json", JsonConvert.SerializeObject(body, Json), ParameterType.RequestBody);
        return request;
    }

    protected static StatusReported Status(string? number, string? carrierRef, string? sscc, string milestone, DateTimeOffset at, string? location, string? locationText, string source, string? remarks) =>
        new(IntegrationEvent.NewId(), at, number, carrierRef, sscc, milestone, location, locationText, source, remarks);
}
