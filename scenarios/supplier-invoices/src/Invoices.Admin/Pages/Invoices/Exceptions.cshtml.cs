using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Wingtip.Invoices.Admin.Services;
using Wingtip.Invoices.Core;

namespace Wingtip.Invoices.Admin.Pages.Invoices;

public class ExceptionsModel : PageModel
{
    private readonly HubData data;

    public ExceptionsModel(HubData data)
    {
        this.data = data;
    }

    [BindProperty(SupportsGet = true)]
    public string Code { get; set; }

    public IList<IssueCount> Issues { get; private set; }
    public IList<InvoiceRow> Rows { get; private set; }
    public int Total { get; private set; }

    public async Task OnGetAsync()
    {
        Issues = await data.OpenIssueCountsAsync();
        var search = new InvoiceSearch { Status = (byte)InvoiceStatus.Exception, PageSize = 200 };
        (Rows, Total) = await data.SearchAsync(search, string.IsNullOrEmpty(Code) ? null : Code);
    }
}
