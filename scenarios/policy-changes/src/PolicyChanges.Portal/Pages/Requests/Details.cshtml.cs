using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PolicyChanges.Data.Entities;
using PolicyChanges.Data.Services;
using PolicyChanges.Portal.Services;

namespace PolicyChanges.Portal.Pages.Requests;

public class DetailsModel : PageModel
{
    private readonly ChangeService _changes;
    private readonly EvidenceStore _evidence;
    private readonly IAuthorizationService _authorization;

    public DetailsModel(ChangeService changes, EvidenceStore evidence, IAuthorizationService authorization)
    {
        _changes = changes;
        _evidence = evidence;
        _authorization = authorization;
    }

    public ChangeRequest Change { get; private set; } = null!;
    public bool IsLead { get; private set; }

    public async Task<IActionResult> OnGetAsync(string reference, CancellationToken ct)
    {
        var r = await _changes.FindAsync(reference, ct);
        if (r is null) return NotFound();
        Change = r;
        IsLead = (await _authorization.AuthorizeAsync(User, Policies.Leads)).Succeeded;
        return Page();
    }

    public async Task<IActionResult> OnGetEvidenceAsync(string reference, long id, CancellationToken ct)
    {
        var r = await _changes.FindAsync(reference, ct);
        var item = r?.Evidence.FirstOrDefault(e => e.Id == id);
        if (item is null) return NotFound();
        var stream = _evidence.Open(item.RelativePath);
        return stream is null ? NotFound() : File(stream, "image/png");
    }

    public async Task<IActionResult> OnPostRequeueAsync(string reference, CancellationToken ct)
    {
        if (!(await _authorization.AuthorizeAsync(User, Policies.Leads)).Succeeded) return Forbid();
        var ok = await _changes.RequeueAsync(reference, User.Identity?.Name ?? "unknown", ct);
        TempData[ok ? "Message" : "Error"] = ok ? $"{reference} queued again." : $"{reference} is not waiting for review.";
        return RedirectToPage(new { reference });
    }

    public async Task<IActionResult> OnPostWithdrawAsync(string reference, string reason, CancellationToken ct)
    {
        var r = await _changes.FindAsync(reference, ct);
        if (r is null) return NotFound();
        var isLead = (await _authorization.AuthorizeAsync(User, Policies.Leads)).Succeeded;
        if (!isLead && !string.Equals(r.SubmittedBy, User.Identity?.Name, StringComparison.OrdinalIgnoreCase)) return Forbid();

        var ok = await _changes.WithdrawAsync(reference, User.Identity?.Name ?? "unknown",
            string.IsNullOrWhiteSpace(reason) ? "no reason given" : reason.Trim(), ct);
        TempData[ok ? "Message" : "Error"] = ok ? $"{reference} withdrawn." : $"{reference} can no longer be withdrawn.";
        return RedirectToPage(new { reference });
    }
}
