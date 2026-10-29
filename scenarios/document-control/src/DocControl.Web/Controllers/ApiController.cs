using Litware.DocControl.Data;
using Litware.DocControl.Transmittals;
using Litware.DocControl.Web.Security;
using Litware.DocControl.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Litware.DocControl.Web.Controllers;

/// <summary>JSON API used by the Excel transmittal register add-in.</summary>
[ApiController]
[Route("api")]
public sealed class ApiController : ControllerBase
{
    private readonly DocumentRepository _documents;
    private readonly RevisionRepository _revisions;
    private readonly TransmittalRepository _transmittals;
    private readonly CurrentUser _user;

    public ApiController(DocumentRepository documents, RevisionRepository revisions, TransmittalRepository transmittals, CurrentUser user)
    {
        _documents = documents;
        _revisions = revisions;
        _transmittals = transmittals;
        _user = user;
    }

    public sealed record RegisterRow(string Number, string Title, string Discipline, string Type, string? LatestRevision, string? LatestStatus, string? ApprovedRevision);

    public sealed record RevisionRow(int Id, string Revision, string Status, string FileName, long Size, string SubmittedBy, DateTime SubmittedUtc, string? ReviewedBy, string? Comment);

    public sealed record TransmittalRow(string Number, string Purpose, string Recipient, string Status, int Items, DateTime CreatedUtc, DateTime? IssuedUtc);

    public sealed record CreateTransmittalRequest(string Project, string Purpose, string RecipientCompany, string? RecipientName, string RecipientEmail, string? Notes, List<string> Documents);

    [HttpGet("projects")]
    public async Task<IEnumerable<Project>> Projects() => await _documents.ProjectsAsync();

    [HttpGet("register/{project}")]
    public async Task<IEnumerable<RegisterRow>> Register(string project) =>
        (await _documents.RegisterAsync(project)).Select(d =>
            new RegisterRow(d.Number, d.Title, d.Discipline, d.DocumentType, d.LatestRevision, d.LatestStatus?.ToString(), d.ApprovedRevision));

    [HttpGet("documents/{number}")]
    public async Task<ActionResult<IEnumerable<RevisionRow>>> Document(string number)
    {
        var doc = await _documents.FindAsync(number);
        if (doc is null) return NotFound();
        return Ok((await _revisions.ForDocumentAsync(doc.Id)).Select(r =>
            new RevisionRow(r.Id, r.Code, r.Status.ToString(), r.FileName, r.Size, r.SubmittedBy, r.SubmittedUtc, r.ReviewedBy, r.ReviewComment)));
    }

    [HttpGet("transmittals/{project}")]
    public async Task<IEnumerable<TransmittalRow>> Transmittals(string project) =>
        (await _transmittals.ListAsync(project)).Select(t =>
            new TransmittalRow(t.Number, t.Purpose.Code(), t.RecipientCompany, t.Status.ToString(), t.ItemCount, t.CreatedUtc, t.IssuedUtc));

    [HttpPost("transmittals"), Authorize(Policy = Policies.Controllers)]
    public async Task<ActionResult<TransmittalRow>> CreateTransmittal(CreateTransmittalRequest request)
    {
        var purpose = request.Purpose?.ToUpperInvariant() switch
        {
            "IFR" => TransmittalPurpose.ForReview,
            "IFA" => TransmittalPurpose.ForApproval,
            "IFC" => TransmittalPurpose.ForConstruction,
            "IFI" => TransmittalPurpose.ForInformation,
            _ => (TransmittalPurpose?)null,
        };
        if (purpose is null) return BadRequest(new { errors = new[] { "Purpose must be IFR, IFA, IFC or IFI." } });

        var issuable = await _revisions.IssuableAsync(request.Project, purpose == TransmittalPurpose.ForReview);
        var latest = issuable.GroupBy(r => r.DocumentNumber).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Id).First(), StringComparer.OrdinalIgnoreCase);

        var errors = new List<string>();
        var selected = new List<RevisionRecord>();
        foreach (var number in request.Documents ?? [])
        {
            if (latest.TryGetValue(number.Trim(), out var rev)) selected.Add(rev);
            else errors.Add($"{number} has no revision that can be issued {purpose.Value.Code()}.");
        }
        errors.AddRange(TransmittalRules.Validate(request.Project, purpose.Value,
            selected.Select(r => new TransmittalLine(r.DocumentNumber, r.Code, r.DocumentTitle, r.Status, r.ProjectCode)).ToList()));
        if (!System.Net.Mail.MailAddress.TryCreate(request.RecipientEmail, out _)) errors.Add("Recipient e-mail address is not valid.");
        if (errors.Count > 0) return BadRequest(new { errors });

        var t = await _transmittals.CreateAsync(new TransmittalRecord
        {
            ProjectCode = request.Project,
            Purpose = purpose.Value,
            RecipientCompany = request.RecipientCompany,
            RecipientName = request.RecipientName ?? "",
            RecipientEmail = request.RecipientEmail,
            Notes = request.Notes,
            CreatedBy = _user.Name,
            CreatedByEmail = _user.MailAddress,
        }, selected.Select(r => r.Id).ToList());

        return Ok(new TransmittalRow(t.Number, t.Purpose.Code(), t.RecipientCompany, t.Status.ToString(), t.ItemCount, t.CreatedUtc, t.IssuedUtc));
    }
}
