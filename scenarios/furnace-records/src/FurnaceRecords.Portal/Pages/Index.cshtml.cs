using FurnaceRecords.Data;
using FurnaceRecords.Data.Entities;
using FurnaceRecords.Portal.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FurnaceRecords.Portal.Pages;

public class IndexModel : PageModel
{
    private readonly FurnaceRecordsContext _db;
    private readonly LoadService _loads;
    private readonly DaqClient _daq;
    private readonly TimeZoneInfo _zone;

    public IndexModel(FurnaceRecordsContext db, LoadService loads, DaqClient daq, PortalOptions portal)
    {
        _db = db;
        _loads = loads;
        _daq = daq;
        _zone = TimeZoneInfo.FindSystemTimeZoneById(portal.TimeZone);
    }

    public List<Furnace> Furnaces { get; private set; } = new();
    public IReadOnlyList<FurnaceLive>? Live { get; private set; }
    public List<Load> ToDecide { get; private set; } = new();

    public DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, _zone);

    public async Task OnGetAsync(CancellationToken ct)
    {
        Furnaces = await _db.Furnaces.AsNoTracking().Where(f => f.Active).OrderBy(f => f.Code).ToListAsync(ct);
        Live = await _daq.LiveAsync(ct);
        ToDecide = await _loads.ListAsync(null, LoadStatus.Completed, 50, ct);
    }
}
