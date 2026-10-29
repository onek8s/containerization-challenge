using Litware.DocControl.Data;
using Litware.DocControl.Data.Storage;
using Litware.DocControl.Transmittals;
using Litware.DocControl.Web.Models;
using Litware.DocControl.Web.Security;
using Litware.DocControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Litware.DocControl.Web.Controllers;

public sealed class TransmittalsController : Controller
{
    private readonly DocumentRepository _documents;
    private readonly RevisionRepository _revisions;
    private readonly TransmittalRepository _transmittals;
    private readonly StorageOptions _storage;
    private readonly CurrentUser _user;

    public TransmittalsController(DocumentRepository documents, RevisionRepository revisions, TransmittalRepository transmittals,
        IOptions<StorageOptions> storage, CurrentUser user)
    {
        _documents = documents;
        _revisions = revisions;
        _transmittals = transmittals;
        _storage = storage.Value;
        _user = user;
    }

    public async Task<IActionResult> Index(string? project) => View(await _transmittals.ListAsync(project));

    [HttpGet, Authorize(Policy = Policies.Controllers)]
    public async Task<IActionResult> Create(string? project, TransmittalPurpose purpose = TransmittalPurpose.ForInformation)
    {
        var model = new CreateTransmittalViewModel { Project = project ?? "", Purpose = purpose };
        await FillAsync(model);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Controllers)]
    public async Task<IActionResult> Create(CreateTransmittalViewModel model, string? refresh)
    {
        if (refresh is not null || string.IsNullOrEmpty(model.Project))
        {
            ModelState.Clear();
            await FillAsync(model);
            return View(model);
        }

        if (string.IsNullOrWhiteSpace(model.RecipientCompany)) ModelState.AddModelError(nameof(model.RecipientCompany), "Enter the recipient company.");
        if (!System.Net.Mail.MailAddress.TryCreate(model.RecipientEmail, out _)) ModelState.AddModelError(nameof(model.RecipientEmail), "Enter a valid e-mail address.");

        var selected = await _revisions.GetManyAsync(model.RevisionIds);
        var lines = selected.Select(r => new TransmittalLine(r.DocumentNumber, r.Code, r.DocumentTitle, r.Status, r.ProjectCode)).ToList();
        foreach (var e in TransmittalRules.Validate(model.Project, model.Purpose, lines)) ModelState.AddModelError("", e);

        if (!ModelState.IsValid)
        {
            await FillAsync(model);
            return View(model);
        }

        var created = await _transmittals.CreateAsync(new TransmittalRecord
        {
            ProjectCode = model.Project,
            Purpose = model.Purpose,
            RecipientCompany = model.RecipientCompany.Trim(),
            RecipientName = model.RecipientName?.Trim() ?? "",
            RecipientEmail = model.RecipientEmail.Trim(),
            Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
            CreatedBy = _user.Name,
            CreatedByEmail = _user.MailAddress,
        }, selected.Select(r => r.Id).ToList());

        TempData["Message"] = $"{created.Number} queued. The package is built and mailed within a few minutes.";
        return RedirectToAction(nameof(Details), new { id = created.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var t = await _transmittals.GetAsync(id);
        if (t is null) return NotFound();
        var items = await _revisions.GetManyAsync(await _transmittals.ItemsAsync(id));
        return View(new TransmittalDetailsViewModel
        {
            Transmittal = t,
            Items = items,
            PackageAvailable = t.PackagePath is not null && System.IO.File.Exists(PackageFullPath(t.PackagePath)),
        });
    }

    public async Task<IActionResult> Download(int id)
    {
        var t = await _transmittals.GetAsync(id);
        if (t?.PackagePath is null) return NotFound();
        var path = PackageFullPath(t.PackagePath);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(Path.GetFullPath(path), "application/zip", t.Number + ".zip", enableRangeProcessing: true);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Controllers)]
    public async Task<IActionResult> Requeue(int id)
    {
        TempData["Message"] = await _transmittals.RequeueAsync(id) ? "Transmittal queued again." : "Only failed transmittals can be queued again.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Controllers)]
    public async Task<IActionResult> Cancel(int id)
    {
        TempData["Message"] = await _transmittals.CancelAsync(id) ? "Transmittal cancelled." : "The transmittal can no longer be cancelled.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private string PackageFullPath(string relative) =>
        Path.Combine(_storage.TransmittalRoot, relative.Replace('/', Path.DirectorySeparatorChar));

    private async Task FillAsync(CreateTransmittalViewModel model)
    {
        model.Projects = await _documents.ProjectsAsync();
        if (string.IsNullOrEmpty(model.Project)) model.Project = model.Projects.FirstOrDefault()?.Code ?? "";
        var all = await _revisions.IssuableAsync(model.Project, model.Purpose == TransmittalPurpose.ForReview);
        // One line per document: the latest issuable revision
        model.Issuable = all.GroupBy(r => r.DocumentId).Select(g => g.OrderByDescending(r => r.Id).First()).OrderBy(r => r.DocumentNumber).ToList();
    }
}
