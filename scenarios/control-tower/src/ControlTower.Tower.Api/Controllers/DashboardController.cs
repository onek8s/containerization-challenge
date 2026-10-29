using Microsoft.AspNetCore.Mvc;
using Proseware.ControlTower.Tower.Models;
using Proseware.ControlTower.Tower.Services;

namespace Proseware.ControlTower.Tower.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboard;

    public DashboardController(DashboardService dashboard) => _dashboard = dashboard;

    /// <summary>Headline figures for the overview page.</summary>
    [HttpGet("summary")]
    public Task<DashboardSummary> Summary(CancellationToken ct) => _dashboard.GetSummaryAsync(ct);

    /// <summary>Vehicles, open legs and network locations for the live map.</summary>
    [HttpGet("map")]
    public Task<MapData> Map(CancellationToken ct) => _dashboard.GetMapAsync(ct);
}
