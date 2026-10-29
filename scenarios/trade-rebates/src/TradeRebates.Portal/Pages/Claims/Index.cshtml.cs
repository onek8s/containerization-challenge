using ContosoFoods.TradeRebates.Core;
using ContosoFoods.TradeRebates.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ContosoFoods.TradeRebates.Portal.Pages.Claims;

public class IndexModel(IPortalStore store) : PortalPageModel
{
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public int? DistributorId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Period { get; set; }

    public IReadOnlyList<ClaimRow> Claims { get; private set; } = [];
    public IEnumerable<SelectListItem> DistributorItems { get; private set; } = [];

    public async Task OnGetAsync()
    {
        int? period = Periods.TryParse(Period, out int p) ? p : null;
        Claims = await store.ListClaimsAsync(string.IsNullOrEmpty(Status) ? null : Status, DistributorId, period);
        DistributorItems = (await store.ListDistributorsAsync()).Select(d => new SelectListItem(d.Name, d.DistributorId.ToString()));
    }
}
