using FurnaceRecords.Data;
using FurnaceRecords.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FurnaceRecords.Portal.Pages.Loads;

public class PlanModel : PageModel
{
    private readonly FurnaceRecordsContext _db;
    private readonly LoadService _loads;

    public PlanModel(FurnaceRecordsContext db, LoadService loads)
    {
        _db = db;
        _loads = loads;
    }

    public sealed class PartInput
    {
        public string? WorkOrder { get; set; }
        public string? PartNumber { get; set; }
        public string? Lot { get; set; }
        public int Quantity { get; set; }
    }

    [BindProperty] public LoadKind Kind { get; set; }
    [BindProperty] public string FurnaceCode { get; set; } = "";
    [BindProperty] public string RecipeCode { get; set; } = "";
    [BindProperty] public List<PartInput> Parts { get; set; } = new();

    public List<SelectListItem> Furnaces { get; private set; } = new();
    public List<SelectListItem> Recipes { get; private set; } = new();
    public string? Error { get; private set; }

    public async Task OnGetAsync(CancellationToken ct) => await LoadListsAsync(ct);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var parts = Parts
            .Where(p => !string.IsNullOrWhiteSpace(p.WorkOrder) || !string.IsNullOrWhiteSpace(p.PartNumber))
            .Select(p => new PlannedPart(p.WorkOrder ?? "", p.PartNumber ?? "", p.Lot, p.Quantity))
            .ToList();
        try
        {
            var load = await _loads.PlanAsync(new PlanLoadRequest(FurnaceCode, RecipeCode, parts, User.Identity!.Name!, Kind), ct);
            return RedirectToPage("Details", new { id = load.LoadNumber });
        }
        catch (LoadException ex)
        {
            Error = ex.Message;
            await LoadListsAsync(ct);
            return Page();
        }
    }

    private async Task LoadListsAsync(CancellationToken ct)
    {
        Furnaces = await _db.Furnaces.AsNoTracking().Where(f => f.Active).OrderBy(f => f.Code)
            .Select(f => new SelectListItem($"{f.Code} {f.Name} (class {f.FurnaceClass})", f.Code)).ToListAsync(ct);
        Recipes = await _db.Recipes.AsNoTracking().Where(r => r.Active).OrderBy(r => r.Code)
            .Select(r => new SelectListItem($"{r.Code} {r.Name}", r.Code)).ToListAsync(ct);
        while (Parts.Count < 5)
        {
            Parts.Add(new PartInput());
        }
    }
}
