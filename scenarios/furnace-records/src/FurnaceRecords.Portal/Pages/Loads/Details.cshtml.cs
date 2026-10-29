using FurnaceRecords.Core;
using FurnaceRecords.Data;
using FurnaceRecords.Data.Entities;
using FurnaceRecords.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FurnaceRecords.Portal.Pages.Loads;

public class DetailsModel : PageModel
{
    private readonly LoadService _loads;
    private readonly PortalOptions _portal;
    private readonly TimeZoneInfo _zone;

    public DetailsModel(LoadService loads, PortalOptions portal)
    {
        _loads = loads;
        _portal = portal;
        _zone = TimeZoneInfo.FindSystemTimeZoneById(portal.TimeZone);
    }

    public Load Load { get; private set; } = null!;
    public IReadOnlyList<LoadEvent> Events { get; private set; } = Array.Empty<LoadEvent>();
    public IReadOnlyList<Finding> Findings { get; private set; } = Array.Empty<Finding>();
    public IReadOnlyList<SurveySensorResult> Survey { get; private set; } = Array.Empty<SurveySensorResult>();
    public string Curve { get; private set; } = "";

    public string ChannelName(int channel) => Load.Furnace!.Channels.FirstOrDefault(c => c.Channel == channel)?.Name ?? "";
    public bool CanDecide => User.IsInRole(_portal.QualityGroup);
    [TempData] public string? Message { get; set; }
    public string? Error { get; private set; }

    public DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, _zone);

    public async Task<IActionResult> OnGetAsync(string id, CancellationToken ct) =>
        await LoadAsync(id, ct) ? Page() : NotFound();

    public Task<IActionResult> OnPostReleaseAsync(string id, string? comment, string rowVersion, CancellationToken ct) =>
        DecideAsync(id, true, comment, rowVersion, ct);

    public Task<IActionResult> OnPostRejectAsync(string id, string? comment, string rowVersion, CancellationToken ct) =>
        DecideAsync(id, false, comment, rowVersion, ct);

    private async Task<IActionResult> DecideAsync(string id, bool release, string? comment, string rowVersion, CancellationToken ct)
    {
        if (!CanDecide)
        {
            return Forbid();
        }
        var version = new byte[64];
        if (!Convert.TryFromBase64String(rowVersion ?? "", version, out var length))
        {
            return BadRequest();
        }
        try
        {
            await _loads.DecideAsync(id, release, User.Identity!.Name!, comment, version[..length], ct);
            Message = release ? "Load released." : "Load rejected.";
            return RedirectToPage(new { id });
        }
        catch (LoadException ex)
        {
            Error = ex.Message;
            return await LoadAsync(id, ct) ? Page() : NotFound();
        }
    }

    private async Task<bool> LoadAsync(string id, CancellationToken ct)
    {
        var load = await _loads.FindAsync(id, ct);
        if (load is null)
        {
            return false;
        }
        Load = load;
        Events = await _loads.EventsAsync(load.Id, ct);
        Findings = LoadService.Findings(load);
        Survey = LoadService.SurveySensors(load);
        var role = load.Kind == LoadKind.Survey ? ChannelRole.Survey : ChannelRole.Load;
        var shown = load.Furnace!.Channels.Where(c => c.Role == role || c.Role == ChannelRole.Control).ToDictionary(c => c.Channel, c => c.Name);
        var samples = (await _loads.SamplesAsync(load.Id, ct)).Where(s => shown.ContainsKey(s.Channel)).ToList();
        Curve = CurveSvg.Render(samples, shown, load.Recipe!.ToSpec(), load.SoakStartUtc, load.SoakEndUtc, _zone);
        return true;
    }
}
