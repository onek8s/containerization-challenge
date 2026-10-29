using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PartsCatalog.Models;
using PartsCatalog.Services;

namespace PartsCatalog.Pages.Parts;

public class CreateModel : PageModel
{
    private readonly PartService _svc;
    private readonly ILogger<CreateModel> _log;

    public CreateModel(PartService svc, ILogger<CreateModel> log)
    {
        _svc = svc;
        _log = log;
    }

    [BindProperty]
    public Part Part { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (await _svc.PartNumberExistsAsync(Part.PartNumber.Trim().ToUpperInvariant()))
        {
            ModelState.AddModelError("Part.PartNumber", "Part number already exists.");
            return Page();
        }

        await _svc.AddAsync(Part);
        _log.LogInformation("Part {PartNumber} created", Part.PartNumber);
        return RedirectToPage("Index");
    }
}
