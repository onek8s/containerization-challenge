using FurnaceRecords.Data;
using FurnaceRecords.Data.Entities;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FurnaceRecords.Portal.Pages.Furnaces;

public class IndexModel : PageModel
{
    private readonly FurnaceRecordsContext _db;

    public IndexModel(FurnaceRecordsContext db) => _db = db;

    public List<Furnace> Furnaces { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken ct) =>
        Furnaces = await _db.Furnaces.AsNoTracking().Include(f => f.Channels).ThenInclude(c => c.Corrections)
            .AsSplitQuery().OrderBy(f => f.Code).ToListAsync(ct);
}
