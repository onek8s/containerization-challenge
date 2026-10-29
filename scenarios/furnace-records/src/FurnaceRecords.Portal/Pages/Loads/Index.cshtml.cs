using FurnaceRecords.Data;
using FurnaceRecords.Data.Entities;
using FurnaceRecords.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FurnaceRecords.Portal.Pages.Loads;

public class IndexModel : PageModel
{
    private readonly FurnaceRecordsContext _db;
    private readonly LoadService _loads;
    private readonly TimeZoneInfo _zone;

    public IndexModel(FurnaceRecordsContext db, LoadService loads, PortalOptions portal)
    {
        _db = db;
        _loads = loads;
        _zone = TimeZoneInfo.FindSystemTimeZoneById(portal.TimeZone);
    }

    [BindProperty(SupportsGet = true)] public string? Furnace { get; set; }
    [BindProperty(SupportsGet = true)] public LoadStatus? Status { get; set; }

    public List<Load> Loads { get; private set; } = new();
    public List<SelectListItem> FurnaceOptions { get; private set; } = new();

    public DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, _zone);

    public async Task OnGetAsync(CancellationToken ct)
    {
        FurnaceOptions = await _db.Furnaces.AsNoTracking().OrderBy(f => f.Code)
            .Select(f => new SelectListItem(f.Code + " " + f.Name, f.Code, f.Code == Furnace)).ToListAsync(ct);
        Loads = await _loads.ListAsync(Furnace, Status, 200, ct);
    }
}
