using Litware.DocControl.Data;
using Litware.DocControl.Web.Models;
using Litware.DocControl.Web.Security;
using Litware.DocControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Litware.DocControl.Web.Controllers;

public sealed class RevisionsController : Controller
{
    private readonly RevisionRepository _revisions;
    private readonly RevisionUploadService _upload;
    private readonly CurrentUser _user;

    public RevisionsController(RevisionRepository revisions, RevisionUploadService upload, CurrentUser user)
    {
        _revisions = revisions;
        _upload = upload;
        _user = user;
    }

    [HttpGet]
    public IActionResult Upload(string? number, string? revision) =>
        View(new UploadViewModel { DocumentNumber = number ?? "", Revision = revision ?? "" });

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> Upload(UploadViewModel model, CancellationToken ct)
    {
        if (model.File is null || model.File.Length == 0)
        {
            ModelState.AddModelError(nameof(model.File), "Choose a file.");
            return View(model);
        }

        var result = await _upload.UploadAsync(model.DocumentNumber, model.Revision, model.Title, model.File, ct);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Error!);
            return View(model);
        }

        TempData["Message"] = $"{result.DocumentNumber} {model.Revision.ToUpperInvariant()} submitted for review.";
        return RedirectToAction("Document", "Register", new { number = result.DocumentNumber });
    }

    [Authorize(Policy = Policies.Controllers)]
    public async Task<IActionResult> Review(string? project) => View(await _revisions.PendingReviewAsync(project));

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Controllers)]
    public async Task<IActionResult> Approve(int id, string? comment)
    {
        var rev = await _revisions.GetAsync(id);
        if (rev is null) return NotFound();
        if (string.Equals(rev.SubmittedBy, _user.Name, StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "You cannot approve a revision you submitted.";
            return RedirectToAction(nameof(Review));
        }

        TempData["Message"] = await _revisions.ApproveAsync(id, _user.Name, comment)
            ? $"{rev.DocumentNumber} {rev.Code} approved."
            : $"{rev.DocumentNumber} {rev.Code} was already reviewed.";
        return RedirectToAction(nameof(Review));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Controllers)]
    public async Task<IActionResult> Reject(int id, string comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            TempData["Error"] = "Enter the reason for the rejection.";
            return RedirectToAction(nameof(Review));
        }

        var rev = await _revisions.GetAsync(id);
        if (rev is null) return NotFound();
        TempData["Message"] = await _revisions.RejectAsync(id, _user.Name, comment.Trim())
            ? $"{rev.DocumentNumber} {rev.Code} rejected."
            : $"{rev.DocumentNumber} {rev.Code} was already reviewed.";
        return RedirectToAction(nameof(Review));
    }
}
