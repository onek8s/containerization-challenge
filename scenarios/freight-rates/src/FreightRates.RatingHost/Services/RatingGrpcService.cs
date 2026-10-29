using FreightRates.Contracts;
using FreightRates.Core;
using FreightRates.RatingHost.Engine;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace FreightRates.RatingHost.Services;

public sealed class RatingGrpcService : Rating.RatingBase
{
    private readonly IHostedRatingEngine _engine;
    private readonly ILogger<RatingGrpcService> _logger;

    public RatingGrpcService(IHostedRatingEngine engine, ILogger<RatingGrpcService> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    public override async Task<RateReply> Rate(RateRequest request, ServerCallContext context)
    {
        var shipment = new ShipmentRequest
        {
            OriginZip = request.OriginZip,
            DestinationZip = request.DestinationZip,
            ShipDate = RatingMapping.FromDateInt(request.ShipDate),
            Lines = request.Lines
                .Select(l => new ShipmentLine { FreightClass = RatingMapping.ClassFromX10(l.FreightClassX10), WeightLbs = l.WeightLbs })
                .ToList(),
        };

        var result = await Execute(() => _engine.RateAsync(shipment, context.CancellationToken));

        var reply = new RateReply
        {
            GrossChargeCents = ToCents(result.GrossCharge),
            MinimumChargeCents = ToCents(result.TariffMinimumCharge),
            DeficitWeightLbs = result.DeficitWeightLbs,
            DeficitChargeCents = ToCents(result.DeficitCharge),
            TransitDays = result.TransitDays,
            TariffName = result.TariffName,
            OriginTerminal = result.OriginTerminal,
            DestinationTerminal = result.DestinationTerminal,
        };
        reply.Lines.AddRange(result.Lines.Select(l => new RateLineResult
        {
            FreightClassX10 = RatingMapping.ClassToX10(l.FreightClass),
            WeightLbs = l.WeightLbs,
            RatePerCwtCents = (int)ToCents(l.RatePerCwt),
            ChargeCents = ToCents(l.Charge),
        }));
        return reply;
    }

    public override async Task<TariffInfoReply> GetTariffInfo(Empty request, ServerCallContext context) =>
        ToReply(await Execute(() => _engine.GetTariffInfoAsync(context.CancellationToken)));

    public override async Task<TariffInfoReply> ReloadTariffs(Empty request, ServerCallContext context) =>
        ToReply(await Execute(() => _engine.ReloadTariffsAsync(context.CancellationToken)));

    private async Task<T> Execute<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (RatingException ex)
        {
            var code = ex.Kind switch
            {
                RatingErrorKind.InvalidShipment => StatusCode.InvalidArgument,
                RatingErrorKind.PointNotServed => StatusCode.NotFound,
                _ => StatusCode.Unavailable,
            };
            if (code == StatusCode.Unavailable)
                _logger.LogError(ex, "Rating engine error {Code}", ex.Code);
            var trailers = new Metadata { { "mrs-code", ex.Code.ToString() } };
            throw new RpcException(new Status(code, ex.Message), trailers);
        }
        catch (TaskCanceledException)
        {
            throw new RpcException(new Status(StatusCode.DeadlineExceeded, "Rating engine did not respond in time."));
        }
    }

    private static TariffInfoReply ToReply(TariffInfo info) => new()
    {
        TariffName = info.TariffName,
        EffectiveDate = RatingMapping.ToDateInt(info.EffectiveDate),
        EngineVersion = info.EngineVersion,
        KeySerial = info.KeySerial,
        LicenceDaysLeft = info.LicenceDaysLeft,
    };

    private static long ToCents(decimal amount) => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
}
