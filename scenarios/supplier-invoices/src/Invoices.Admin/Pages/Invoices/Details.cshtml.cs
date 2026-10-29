using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Wingtip.Invoices.Admin.Services;
using Wingtip.Invoices.Core;

namespace Wingtip.Invoices.Admin.Pages.Invoices;

public class DetailsModel : PageModel
{
    private readonly HubData data;
    private readonly ProcessorClient processor;
    private readonly CurrentUser user;
    private readonly ILogger<DetailsModel> logger;

    public DetailsModel(HubData data, ProcessorClient processor, CurrentUser user, ILogger<DetailsModel> logger)
    {
        this.data = data;
        this.processor = processor;
        this.user = user;
        this.logger = logger;
    }

    public InvoiceRow Invoice { get; private set; }
    public IList<LineRow> Lines { get; private set; }
    public IList<IssueRow> Issues { get; private set; }
    public IList<HistoryRow> History { get; private set; }

    public async Task<IActionResult> OnGetAsync(long id)
    {
        Invoice = await data.InvoiceAsync(id);
        if (Invoice == null) return NotFound();
        (Lines, Issues, History) = await data.InvoiceDetailsAsync(id);
        return Page();
    }

    public Task<IActionResult> OnPostRematchAsync(long id) => Act(id, async () =>
    {
        var result = await processor.RematchAsync(id, user.Account);
        return result.Matched ? "The invoice now matches the order. Release it for posting." : "Still not matching: " + result.Summary;
    });

    public Task<IActionResult> OnPostReleaseAsync(long id, string comment) => Act(id, async () =>
    {
        var invoice = await data.InvoiceAsync(id);
        if (invoice.StatusValue != InvoiceStatus.Matched && string.IsNullOrWhiteSpace(comment))
            throw new ProcessorException("Write a comment that explains why the exceptions are accepted.", null);
        await processor.ReleaseAsync(id, user.Account, comment);
        return "Released for posting.";
    });

    public Task<IActionResult> OnPostRejectAsync(long id, string reason) => Act(id, async () =>
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ProcessorException("A reason is required.", null);
        await data.RejectAsync(id, user.Account, reason);
        return "Rejected.";
    });

    public Task<IActionResult> OnPostCommentAsync(long id, string comment) => Act(id, async () =>
    {
        if (!string.IsNullOrWhiteSpace(comment)) await data.AddCommentAsync(id, user.Account, comment.Trim());
        return null;
    });

    private async Task<IActionResult> Act(long id, Func<Task<string>> action)
    {
        try
        {
            TempData["Message"] = await action();
        }
        catch (ProcessorException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (SqlException ex) when (ex.Number >= 50000)
        {
            TempData["Error"] = ex.Message;
        }
        logger.LogInformation("{User} {Handler} invoice {InvoiceId}", user.Account, Request.Query["handler"].ToString(), id);
        return RedirectToPage(new { id });
    }
}
