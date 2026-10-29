using BatchRelease.Contracts;
using BatchRelease.Core.Model;
using BatchRelease.Core.Rules;
using BatchRelease.Core.Security;
using BatchRelease.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BatchRelease.Portal.Pages.Batches;

public class DetailsModel : PageModel
{
    private readonly BatchQueries _queries;
    private readonly ISigningHostClient _signing;
    private readonly TimeProvider _clock;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(BatchQueries queries, ISigningHostClient signing, TimeProvider clock, ILogger<DetailsModel> logger)
    {
        _queries = queries;
        _signing = signing;
        _clock = clock;
        _logger = logger;
    }

    public Batch? Batch { get; private set; }
    public Assessment? Assessment { get; private set; }
    public ReleaseRights Rights { get; private set; }
    public bool CanRead => Rights.HasFlag(ReleaseRights.ReadRecord);
    public string? AccessError { get; private set; }
    public List<DecisionKind> AvailableDecisions { get; } = new();

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        Batch = await _queries.GetAsync(id, ct);
        if (Batch is null)
            return NotFound();

        try
        {
            var access = await _signing.GetAccessAsync(id, User.Sid(), ct);
            Rights = (ReleaseRights)access.GrantedMask;
        }
        catch (Exception ex) when (ex is SigningHostException or HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Access check for batch {Batch} failed", Batch.BatchNumber);
            AccessError = "The signing host is not available. Documents and signing are disabled.";
        }

        if (!CanRead)
            return AccessError is null ? Forbid() : Page();

        var rules = new DispositionRules(_clock);
        Assessment = rules.Assess(Batch, Batch.Status == BatchStatus.Reviewed ? DecisionKind.Release : DecisionKind.Review, User.Sid());

        foreach (var kind in Enum.GetValues<DecisionKind>())
        {
            if (!Rights.HasFlag(ReleaseRightsExtensions.RequiredFor(kind)))
                continue;
            var visible = kind switch
            {
                DecisionKind.Review => Batch.Status is BatchStatus.Received or BatchStatus.UnderReview,
                DecisionKind.Release => Batch.Status == BatchStatus.Reviewed,
                DecisionKind.Reject => Batch.Status is not (BatchStatus.Released or BatchStatus.Rejected),
                DecisionKind.Hold => Batch.Status is not (BatchStatus.Released or BatchStatus.Rejected or BatchStatus.OnHold),
                DecisionKind.Resume => Batch.Status == BatchStatus.OnHold,
                _ => false,
            };
            if (visible)
                AvailableDecisions.Add(kind);
        }

        return Page();
    }
}
