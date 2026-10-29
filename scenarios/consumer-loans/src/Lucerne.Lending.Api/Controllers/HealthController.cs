using Lucerne.Lending.CoreBanking;
using Lucerne.Lending.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lucerne.Lending.Api.Controllers;

[ApiController]
[Route("health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    private readonly Db _db;
    private readonly ICoreReferenceData _core;

    public HealthController(Db db, ICoreReferenceData core)
    {
        _db = db;
        _core = core;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var checks = new Dictionary<string, string>();
        try { await _db.ScalarAsync<int>("SELECT COUNT(*) FROM lending.Product", ct: ct); checks["database"] = "ok"; }
        catch (Exception ex) { checks["database"] = ex.Message; }
        try { checks["core"] = "ok, business date " + (await _core.GetBusinessDateAsync(ct)).ToString("yyyy-MM-dd"); }
        catch (Exception ex) { checks["core"] = ex.Message; }
        var healthy = checks.Values.All(v => v.StartsWith("ok"));
        return StatusCode(healthy ? 200 : 503, new { status = healthy ? "Healthy" : "Unhealthy", checks });
    }
}
