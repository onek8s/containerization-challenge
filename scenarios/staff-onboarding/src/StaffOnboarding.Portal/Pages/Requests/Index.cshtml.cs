using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StaffOnboarding.Core.Requests;
using StaffOnboarding.Portal.Data;

namespace StaffOnboarding.Portal.Pages.Requests;

public class IndexModel(OnboardingRepository repository) : PageModel
{
    [BindProperty(SupportsGet = true)] public RequestStatus? Status { get; set; }
    [BindProperty(SupportsGet = true)] public RequestKind? Kind { get; set; }

    public IReadOnlyList<RequestRow> Rows { get; private set; } = Array.Empty<RequestRow>();

    public async Task OnGetAsync() => Rows = await repository.ListRequestsAsync(Status, Kind, null);
}
