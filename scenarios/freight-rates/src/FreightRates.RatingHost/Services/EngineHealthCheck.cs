using FreightRates.Core;
using FreightRates.RatingHost.Engine;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FreightRates.RatingHost.Services;

public sealed class EngineHealthCheck : IHealthCheck
{
    private readonly IHostedRatingEngine _engine;

    public EngineHealthCheck(IHostedRatingEngine engine) => _engine = engine;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var info = await _engine.GetTariffInfoAsync(cancellationToken);
            var data = new Dictionary<string, object>
            {
                ["tariff"] = info.TariffName,
                ["effective"] = info.EffectiveDate.ToString("yyyy-MM-dd"),
                ["engine"] = info.EngineVersion,
                ["keySerial"] = info.KeySerial,
                ["licenceDaysLeft"] = info.LicenceDaysLeft,
            };
            if (info.EngineVersion != "simulator" && info.LicenceDaysLeft <= 14)
                return HealthCheckResult.Degraded($"Licence expires in {info.LicenceDaysLeft} days", data: data);
            return HealthCheckResult.Healthy(data: data);
        }
        catch (RatingException ex)
        {
            return HealthCheckResult.Unhealthy($"{ex.Message} ({ex.Code})");
        }
    }
}
