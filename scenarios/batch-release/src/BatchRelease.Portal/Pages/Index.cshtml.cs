using BatchRelease.Core.Model;
using BatchRelease.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace BatchRelease.Portal.Pages;

public class IndexModel : PageModel
{
    private static readonly BatchStatus[] Open = { BatchStatus.Received, BatchStatus.UnderReview, BatchStatus.Reviewed, BatchStatus.OnHold };
    private static readonly BatchStatus[] Final = { BatchStatus.Released, BatchStatus.Rejected };

    private readonly BatchQueries _queries;
    private readonly PortalOptions _options;

    public IndexModel(BatchQueries queries, IOptions<PortalOptions> options)
    {
        _queries = queries;
        _options = options.Value;
    }

    [BindProperty(SupportsGet = true)]
    public string View { get; set; } = "open";

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    public List<BatchListItem> Batches { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken ct)
    {
        var statuses = View switch
        {
            "final" => Final,
            "all" => null,
            _ => Open,
        };
        Batches = await _queries.ListAsync(statuses, Q, _options.QueueSize, ct);
    }
}
