using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Wingtip.Invoices.Admin.Services;

namespace Wingtip.Invoices.Admin.Pages.Invoices;

public class IndexModel : PageModel
{
    private readonly HubData data;

    public IndexModel(HubData data)
    {
        this.data = data;
    }

    [BindProperty(SupportsGet = true)]
    public InvoiceSearch Search { get; set; } = new();

    public IList<InvoiceRow> Rows { get; private set; }
    public int Total { get; private set; }
    public IList<SupplierRow> Suppliers { get; private set; }

    public async Task OnGetAsync()
    {
        Suppliers = await data.SuppliersAsync();
        (Rows, Total) = await data.SearchAsync(Search);
    }

    public Dictionary<string, string> RouteFor(int page)
    {
        var route = new Dictionary<string, string> { ["page"] = page.ToString(CultureInfo.InvariantCulture) };
        if (Search.Status.HasValue) route["status"] = Search.Status.Value.ToString(CultureInfo.InvariantCulture);
        if (Search.SupplierId.HasValue) route["supplierId"] = Search.SupplierId.Value.ToString(CultureInfo.InvariantCulture);
        if (!string.IsNullOrEmpty(Search.Text)) route["text"] = Search.Text;
        if (Search.From.HasValue) route["from"] = Search.From.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (Search.To.HasValue) route["to"] = Search.To.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return route;
    }
}
