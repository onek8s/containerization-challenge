using FurnaceRecords.Data;
using FurnaceRecords.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FurnaceRecords.Portal.Pages.Recipes;

public class EditModel : PageModel
{
    private readonly FurnaceRecordsContext _db;

    public EditModel(FurnaceRecordsContext db) => _db = db;

    [BindProperty] public Recipe Recipe { get; set; } = new();
    public List<string> Errors { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken ct)
    {
        if (id is null)
        {
            return Page();
        }
        var recipe = await _db.Recipes.FindAsync(new object[] { id.Value }, ct);
        if (recipe is null) return NotFound();
        Recipe = recipe;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        Recipe.Code = (Recipe.Code ?? "").Trim().ToUpperInvariant();
        Errors = Recipe.ToSpec().Validate().ToList();
        if (Recipe.Id == 0 && await _db.Recipes.AnyAsync(r => r.Code == Recipe.Code, ct))
        {
            Errors.Add($"Recipe {Recipe.Code} already exists.");
        }
        if (Errors.Count > 0)
        {
            return Page();
        }

        if (Recipe.Id == 0)
        {
            _db.Recipes.Add(Recipe);
        }
        else
        {
            var existing = await _db.Recipes.FindAsync(new object[] { Recipe.Id }, ct);
            if (existing is null) return NotFound();
            Recipe.Code = existing.Code;
            _db.Entry(existing).CurrentValues.SetValues(Recipe);
        }
        await _db.SaveChangesAsync(ct);
        return RedirectToPage("Index");
    }
}
