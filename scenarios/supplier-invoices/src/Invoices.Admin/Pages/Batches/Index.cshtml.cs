using Microsoft.AspNetCore.Mvc.RazorPages;
using Wingtip.Invoices.Admin.Services;

namespace Wingtip.Invoices.Admin.Pages.Batches;

public class IndexModel : PageModel
{
    private readonly HubData data;

    public IndexModel(HubData data)
    {
        this.data = data;
    }

    public IList<BatchRow> Batches { get; private set; }

    public async Task OnGetAsync()
    {
        Batches = await data.BatchesAsync();
    }
}
