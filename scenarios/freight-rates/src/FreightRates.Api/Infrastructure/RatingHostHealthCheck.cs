using FreightRates.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FreightRates.Api.Infrastructure;

public sealed class RatingHostHealthCheck : IHealthCheck
{
    private readonly IRatingEngine _engine;

    public RatingHostHealthCheck(IRatingEngine engine) => _engine = engine;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var info = await _engine.GetTariffInfoAsync(cancellationToken);
            return HealthCheckResult.Healthy($"{info.TariffName} effective {info.EffectiveDate:yyyy-MM-dd}");
        }
        catch (RatingException ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}
