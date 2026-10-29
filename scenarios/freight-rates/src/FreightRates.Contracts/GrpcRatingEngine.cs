using FreightRates.Core;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FreightRates.Contracts;

/// <summary>Rates shipments through the rating host.</summary>
public sealed class GrpcRatingEngine : IRatingEngine
{
    private readonly Rating.RatingClient _client;
    private readonly RatingHostOptions _options;

    public GrpcRatingEngine(Rating.RatingClient client, IOptions<RatingHostOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task<EngineRateResult> RateAsync(ShipmentRequest shipment, CancellationToken cancellationToken = default)
    {
        var request = RatingMapping.ToRequest(shipment);
        var reply = await Call(() => _client.RateAsync(request, deadline: Deadline(), cancellationToken: cancellationToken).ResponseAsync);
        return RatingMapping.ToResult(reply);
    }

    public async Task<TariffInfo> GetTariffInfoAsync(CancellationToken cancellationToken = default)
    {
        var reply = await Call(() => _client.GetTariffInfoAsync(new Empty(), deadline: Deadline(), cancellationToken: cancellationToken).ResponseAsync);
        return RatingMapping.ToTariffInfo(reply);
    }

    private DateTime Deadline() => DateTime.UtcNow.AddSeconds(_options.TimeoutSeconds);

    private static async Task<T> Call<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (RpcException ex)
        {
            var code = int.TryParse(ex.Trailers.GetValue("mrs-code"), out var c) ? c : 0;
            var kind = ex.StatusCode switch
            {
                StatusCode.InvalidArgument => RatingErrorKind.InvalidShipment,
                StatusCode.NotFound => RatingErrorKind.PointNotServed,
                _ => RatingErrorKind.EngineUnavailable,
            };
            throw new RatingException(kind, code, ex.Status.Detail);
        }
    }
}

public sealed class RatingHostOptions
{
    public const string Section = "RatingHost";

    public string Address { get; set; } = "https://ratehost01.corp.local:5101";
    public int TimeoutSeconds { get; set; } = 20;
}

public static class RatingHostServiceCollectionExtensions
{
    public static IServiceCollection AddRatingHostClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RatingHostOptions>(configuration.GetSection(RatingHostOptions.Section));
        services.AddGrpcClient<Rating.RatingClient>((sp, o) =>
            o.Address = new Uri(sp.GetRequiredService<IOptions<RatingHostOptions>>().Value.Address));
        services.AddScoped<IRatingEngine, GrpcRatingEngine>();
        return services;
    }
}
