using Microsoft.AspNetCore.Mvc.RazorPages;
using Wingtip.Invoices.Admin.Services;

namespace Wingtip.Invoices.Admin.Pages.Suppliers;

public class IndexModel : PageModel
{
    private readonly HubData data;

    public IndexModel(HubData data)
    {
        this.data = data;
    }

    public IList<SupplierRow> Suppliers { get; private set; }
    public IList<ChannelRow> Channels { get; private set; }

    public async Task OnGetAsync()
    {
        Suppliers = await data.SuppliersAsync();
        Channels = await data.ChannelsAsync();
    }
}
