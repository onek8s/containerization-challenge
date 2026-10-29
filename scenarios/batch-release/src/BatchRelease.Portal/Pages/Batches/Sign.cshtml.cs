using BatchRelease.Contracts;
using BatchRelease.Core.Model;
using BatchRelease.Core.Rules;
using BatchRelease.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BatchRelease.Portal.Pages.Batches;

public class SignModel : PageModel
{
    private readonly BatchQueries _queries;
    private readonly ISigningHostClient _signing;

    public SignModel(BatchQueries queries, ISigningHostClient signing)
    {
        _queries = queries;
        _signing = signing;
    }

    public Batch? Batch { get; private set; }

    [BindProperty(SupportsGet = true)]
    public DecisionKind Kind { get; set; }

    [BindProperty]
    public SignInput Input { get; set; } = new();

    public string? Error { get; private set; }
    public List<Finding> Findings { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        Batch = await _queries.GetAsync(id, ct);
        return Batch is null || !Enum.IsDefined(Kind) ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken ct)
    {
        Batch = await _queries.GetAsync(id, ct);
        if (Batch is null || !Enum.IsDefined(Kind))
            return NotFound();

        var result = await _signing.DecideAsync(new DecisionRequest
        {
            BatchId = id,
            Kind = Kind,
            AccountName = User.Identity!.Name!,
            Password = Input.Password ?? "",
            MeaningConfirmed = Input.MeaningConfirmed,
            Comment = Input.Comment,
            Workstation = HttpContext.Connection.RemoteIpAddress?.ToString(),
        }, ct);

        Input.Password = null;
        if (!result.Succeeded)
        {
            Error = result.Problem?.Message ?? "The signature was not accepted.";
            Findings = result.Problem?.Findings ?? new();
            return Page();
        }

        var d = result.Decision!;
        TempData["Message"] = d.CertificateNumber is null
            ? $"{Kind} of batch {Batch.BatchNumber} signed. Status: {d.BatchStatus}."
            : $"{Kind} of batch {Batch.BatchNumber} signed. Certificate {d.CertificateNumber} issued.";
        return RedirectToPage("/Batches/Details", new { id });
    }

    public sealed class SignInput
    {
        public bool MeaningConfirmed { get; set; }
        public string? Comment { get; set; }
        public string? Password { get; set; }
    }
}
