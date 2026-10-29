using Microsoft.AspNetCore.Mvc;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Tower.Models;
using Proseware.ControlTower.Tower.Services;

namespace Proseware.ControlTower.Tower.Controllers;

[ApiController]
[Route("api/kpi")]
public sealed class KpiController : ControllerBase
{
    private readonly KpiService _kpi;
    private readonly TerminalWorkloadService _terminals;
    private readonly IClock _clock;

    public KpiController(KpiService kpi, TerminalWorkloadService terminals, IClock clock)
    {
        _kpi = kpi;
        _terminals = terminals;
        _clock = clock;
    }

    /// <summary>Daily KPI series, grouped by total, mode, customer or carrier.</summary>
    [HttpGet("daily")]
    public Task<IReadOnlyList<KpiPoint>> Daily([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] KpiGrouping groupBy = KpiGrouping.Total,
        [FromQuery] int? customerId = null, CancellationToken ct = default)
    {
        var today = _clock.UtcNow.UtcDateTime.Date;
        return _kpi.SeriesAsync(from ?? today.AddDays(-30), to ?? today, groupBy, customerId, ct);
    }

    [HttpGet("carriers")]
    public Task<IReadOnlyList<CarrierScore>> Carriers([FromQuery] int days = 30, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        return _kpi.CarrierScorecardAsync(now.AddDays(-Math.Clamp(days, 1, 365)), now.AddDays(1), ct);
    }

    [HttpGet("terminals")]
    public Task<IReadOnlyList<TerminalWorkload>> Terminals([FromQuery] int hours = 24, CancellationToken ct = default) =>
        _terminals.GetAsync(hours, ct);
}
