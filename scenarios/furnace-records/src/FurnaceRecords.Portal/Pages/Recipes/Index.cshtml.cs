using FurnaceRecords.Data;
using FurnaceRecords.Data.Entities;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FurnaceRecords.Portal.Pages.Recipes;

public class IndexModel : PageModel
{
    private readonly FurnaceRecordsContext _db;

    public IndexModel(FurnaceRecordsContext db) => _db = db;

    public List<Recipe> Recipes { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken ct) =>
        Recipes = await _db.Recipes.AsNoTracking().OrderBy(r => r.Code).ToListAsync(ct);
}
