using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PartsCatalog.Models;
using PartsCatalog.Services;

namespace PartsCatalog.Pages.Parts;

public class IndexModel : PageModel
{
    private readonly PartService _svc;

    public IndexModel(PartService svc)
    {
        _svc = svc;
    }

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Category { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Reorder { get; set; }

    public List<Part> Parts { get; private set; } = new();
    public List<string> Categories { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Categories = await _svc.GetCategoriesAsync();
        Parts = await _svc.SearchAsync(Q, Category, Reorder);
    }
}
