using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using PolicyChanges.Data.Entities;
using PolicyChanges.Data.Services;
using PolicyChanges.Portal.Services;

namespace PolicyChanges.Portal.Pages;

public class IndexModel : PageModel
{
    private readonly ChangeService _changes;
    private readonly TimeProvider _time;

    public IndexModel(ChangeService changes, TimeProvider time, IOptions<PortalOptions> options)
    {
        _changes = changes;
        _time = time;
        OfflineAfter = TimeSpan.FromMinutes(options.Value.StationOfflineMinutes);
    }

    public List<StatusCount> Counts { get; private set; } = new();
    public List<DeskStation> Stations { get; private set; } = new();
    public List<ChangeRequest> Recent { get; private set; } = new();
    public TimeSpan OfflineAfter { get; }
    public DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

    public async Task OnGetAsync(CancellationToken ct)
    {
        Counts = await _changes.CountsAsync(ct);
        Stations = await _changes.StationsAsync(ct);
        Recent = await _changes.ListAsync(null, null, 15, ct);
    }
}
