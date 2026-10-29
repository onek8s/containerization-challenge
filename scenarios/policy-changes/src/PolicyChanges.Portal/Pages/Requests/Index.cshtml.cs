using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PolicyChanges.Core.Requests;
using PolicyChanges.Data.Entities;
using PolicyChanges.Data.Services;

namespace PolicyChanges.Portal.Pages.Requests;

public class IndexModel : PageModel
{
    private readonly ChangeService _changes;

    public IndexModel(ChangeService changes) => _changes = changes;

    [BindProperty(SupportsGet = true)] public ChangeStatus? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }

    public List<ChangeRequest> Requests { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken ct) =>
        Requests = await _changes.ListAsync(Status, Search, 200, ct);
}
