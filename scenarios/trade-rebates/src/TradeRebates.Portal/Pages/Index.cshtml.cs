using ContosoFoods.TradeRebates.Core;
using ContosoFoods.TradeRebates.Data;

namespace ContosoFoods.TradeRebates.Portal.Pages;

public class IndexModel(IPortalStore store) : PortalPageModel
{
    public Dashboard Dashboard { get; private set; } = new();

    public async Task OnGetAsync() => Dashboard = await store.GetDashboardAsync();
}
