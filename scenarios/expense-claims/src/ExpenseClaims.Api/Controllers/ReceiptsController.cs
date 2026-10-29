using ExpenseClaims.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseClaims.Api.Controllers;

[ApiController]
[Route("api/receipts")]
public sealed class ReceiptsController : ControllerBase
{
    private readonly ClaimService _claims;

    public ReceiptsController(ClaimService claims) => _claims = claims;

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var (receipt, content) = await _claims.OpenReceiptAsync(id, ct);
        return File(content, receipt.ContentType, receipt.FileName);
    }
}
