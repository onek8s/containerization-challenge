using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PartsCatalog.Models;
using PartsCatalog.Services;

namespace PartsCatalog.Pages.Parts;

public class EditModel : PageModel
{
    private readonly PartService _svc;
    private readonly ILogger<EditModel> _log;

    public EditModel(PartService svc, ILogger<EditModel> log)
    {
        _svc = svc;
        _log = log;
    }

    [BindProperty]
    public Part Part { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var part = await _svc.FindAsync(id);
        if (part == null)
        {
            return NotFound();
        }

        Part = part;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        Part.Id = id;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (await _svc.PartNumberExistsAsync(Part.PartNumber.Trim().ToUpperInvariant(), id))
        {
            ModelState.AddModelError("Part.PartNumber", "Part number already exists.");
            return Page();
        }

        await _svc.UpdateAsync(Part);
        _log.LogInformation("Part {PartNumber} updated", Part.PartNumber);
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (await _svc.DeleteAsync(id))
        {
            _log.LogInformation("Part {Id} deleted", id);
        }

        return RedirectToPage("Index");
    }
}
