using FurnaceRecords.Data;
using FurnaceRecords.Data.Entities;
using FurnaceRecords.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FurnaceRecords.Portal.Pages.Loads;

public class CertificateModel : PageModel
{
    private readonly LoadService _loads;
    private readonly TimeZoneInfo _zone;

    public CertificateModel(LoadService loads, PortalOptions portal)
    {
        _loads = loads;
        _zone = TimeZoneInfo.FindSystemTimeZoneById(portal.TimeZone);
    }

    public Load Load { get; private set; } = null!;

    public DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, _zone);

    public async Task<IActionResult> OnGetAsync(string id, CancellationToken ct)
    {
        var load = await _loads.FindAsync(id, ct);
        if (load is null) return NotFound();
        if (load.Status != LoadStatus.Released) return RedirectToPage("Details", new { id });
        Load = load;
        return Page();
    }
}
