using ContosoFoods.TradeRebates.Core;
using ContosoFoods.TradeRebates.Data;
using Microsoft.AspNetCore.Mvc;

namespace ContosoFoods.TradeRebates.Portal.Pages.Audit;

public class IndexModel(IPortalStore store) : PortalPageModel
{
    [BindProperty(SupportsGet = true)] public string? Entity { get; set; }
    [BindProperty(SupportsGet = true)] public string? EntityId { get; set; }

    public IReadOnlyList<AuditRow> Rows { get; private set; } = [];

    public async Task OnGetAsync() =>
        Rows = await store.ListAuditAsync(string.IsNullOrEmpty(Entity) ? null : Entity, string.IsNullOrEmpty(EntityId) ? null : EntityId);
}
