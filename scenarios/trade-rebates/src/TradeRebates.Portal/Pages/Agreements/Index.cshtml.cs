using ContosoFoods.TradeRebates.Core;
using ContosoFoods.TradeRebates.Data;

namespace ContosoFoods.TradeRebates.Portal.Pages.Agreements;

public class IndexModel(IPortalStore store) : PortalPageModel
{
    public IReadOnlyList<AgreementRow> Agreements { get; private set; } = [];

    public async Task OnGetAsync() => Agreements = await store.ListAgreementsAsync(null, null);
}
