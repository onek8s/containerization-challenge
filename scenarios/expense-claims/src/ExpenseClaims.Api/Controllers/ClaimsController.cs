using ExpenseClaims.Api.Models;
using ExpenseClaims.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ExpenseClaims.Api.Controllers;

[ApiController]
[Route("api/claims")]
public sealed class ClaimsController : ControllerBase
{
    private readonly ClaimService _claims;

    public ClaimsController(ClaimService claims) => _claims = claims;

    /// <summary>Claims of the signed-in employee.</summary>
    [HttpGet]
    public Task<IReadOnlyList<ClaimSummaryDto>> List(CancellationToken ct) => _claims.ListMineAsync(ct);

    [HttpGet("{id:int}")]
    public Task<ClaimDto> Get(int id, CancellationToken ct) => _claims.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<ClaimDto>> Create(CreateClaimRequest request, CancellationToken ct)
    {
        var claim = await _claims.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = claim.Id }, claim);
    }

    [HttpPut("{id:int}")]
    public Task<ClaimDto> Update(int id, UpdateClaimRequest request, CancellationToken ct) => _claims.UpdateAsync(id, request, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _claims.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/lines")]
    public Task<ClaimDto> AddLine(int id, ClaimLineRequest request, CancellationToken ct) => _claims.AddLineAsync(id, request, ct);

    [HttpPut("{id:int}/lines/{lineId:int}")]
    public Task<ClaimDto> UpdateLine(int id, int lineId, ClaimLineRequest request, CancellationToken ct) =>
        _claims.UpdateLineAsync(id, lineId, request, ct);

    [HttpDelete("{id:int}/lines/{lineId:int}")]
    public Task<ClaimDto> DeleteLine(int id, int lineId, CancellationToken ct) => _claims.DeleteLineAsync(id, lineId, ct);

    /// <summary>Adds a company card transaction as a new line.</summary>
    [HttpPost("{id:int}/lines/from-card/{transactionId:int}")]
    public Task<ClaimDto> AddCardLine(int id, int transactionId, CancellationToken ct) => _claims.AddCardLineAsync(id, transactionId, ct);

    /// <summary>Uploads the receipt of a line (PDF, JPEG, PNG or HEIC). Replaces an earlier receipt.</summary>
    [HttpPost("{id:int}/lines/{lineId:int}/receipt")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public Task<ClaimDto> UploadReceipt(int id, int lineId, IFormFile file, [FromServices] IOptions<ReceiptStoreOptions> options, CancellationToken ct) =>
        _claims.AttachReceiptAsync(id, lineId, file, options.Value, ct);

    [HttpPost("{id:int}/submit")]
    public Task<ClaimDto> Submit(int id, CancellationToken ct) => _claims.SubmitAsync(id, ct);
}
