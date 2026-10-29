using Lucerne.Lending.Api.Infrastructure;
using Lucerne.Lending.Contracts;
using Lucerne.Lending.Data;
using Lucerne.Lending.Workflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lucerne.Lending.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize(Policy = "Customer")]
public sealed class ApplicationsController : ControllerBase
{
    private readonly ApplicationService _service;
    private readonly ApplicationRepository _applications;
    private readonly DocumentRepository _documents;

    public ApplicationsController(ApplicationService service, ApplicationRepository applications, DocumentRepository documents)
    {
        _service = service;
        _applications = applications;
        _documents = documents;
    }

    [HttpPost("applications")]
    public async Task<ActionResult<ApplicationCreated>> Submit(ApplicationRequest request, CancellationToken ct)
    {
        var created = await _service.SubmitAsync(request, User.OnBehalfOf(), User.Actor(), ct);
        return AcceptedAtAction(nameof(Get), new { reference = created.Reference }, created);
    }

    [HttpGet("applications/{reference}")]
    public async Task<ActionResult<ApplicationDetailDto>> Get(string reference, [FromQuery] string? pic, CancellationToken ct)
    {
        if (!await OwnsAsync(reference, pic, ct)) return NotFound();
        var detail = await _service.GetDetailAsync(reference, ct);
        return detail == null ? NotFound() : detail;
    }

    [HttpGet("customers/{pic}/applications")]
    public Task<IReadOnlyList<ApplicationSummaryDto>> ForCustomer(string pic, CancellationToken ct) =>
        _service.ListForCustomerAsync(pic, ct);

    [HttpPost("applications/{reference}/accept")]
    public async Task<IActionResult> Accept(string reference, AcceptOfferRequest request, [FromQuery] string? pic, CancellationToken ct)
    {
        if (!await OwnsAsync(reference, pic, ct)) return NotFound();
        await _service.AcceptOfferAsync(reference, request, User.Actor(), ct);
        return NoContent();
    }

    [HttpPost("applications/{reference}/cancel")]
    public async Task<IActionResult> Cancel(string reference, [FromQuery] string? pic, CancellationToken ct)
    {
        if (!await OwnsAsync(reference, pic, ct)) return NotFound();
        await _service.CancelAsync(reference, User.Actor(), "Cancelled by customer", ct);
        return NoContent();
    }

    [HttpGet("applications/{reference}/documents/{id:long}")]
    public async Task<IActionResult> Document(string reference, long id, [FromQuery] string? pic, CancellationToken ct)
    {
        if (!await OwnsAsync(reference, pic, ct)) return NotFound();
        var row = await _applications.GetRowAsync(reference, ct);
        var doc = await _documents.GetAsync(id, ct);
        if (row == null || doc?.Content == null || doc.ApplicationId != row.Id) return NotFound();
        return File(doc.Content, doc.ContentType, doc.FileName);
    }

    /// <summary>Portal calls are limited to the applications of the signed-in person (pic = personal identity code).</summary>
    private async Task<bool> OwnsAsync(string reference, string? pic, CancellationToken ct)
    {
        if (!User.HasClaim("scope", ApiScopes.Portal) || User.HasClaim("scope", ApiScopes.Office)) return true;
        if (string.IsNullOrEmpty(pic)) return false;
        var record = await _applications.GetAsync(reference, ct);
        return record != null && record.Applicants.Any(a => string.Equals(a.PersonalIdentityCode, pic, StringComparison.OrdinalIgnoreCase));
    }
}
