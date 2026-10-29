using ExpenseClaims.Api.Authentication;
using ExpenseClaims.Api.Models;
using ExpenseClaims.Api.Services;
using ExpenseClaims.Core.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseClaims.Api.Controllers;

[ApiController]
[Route("api/finance")]
[Authorize(Policy = Policies.Finance)]
public sealed class FinanceController : ControllerBase
{
    private readonly ClaimService _claims;

    public FinanceController(ClaimService claims) => _claims = claims;

    /// <summary>Claims in the given status, oldest decision first. Defaults to approved claims waiting for the finance check.</summary>
    [HttpGet("claims")]
    public Task<IReadOnlyList<ClaimSummaryDto>> Claims([FromQuery] ClaimStatus status = ClaimStatus.Approved, CancellationToken ct = default) =>
        _claims.ListForFinanceAsync(status, ct);

    [HttpPost("claims/{id:int}/check")]
    public Task<ClaimDto> Check(int id, DecisionRequest request, CancellationToken ct) => _claims.FinanceCheckAsync(id, true, request, ct);

    [HttpPost("claims/{id:int}/return")]
    public Task<ClaimDto> Return(int id, DecisionRequest request, CancellationToken ct) => _claims.FinanceCheckAsync(id, false, request, ct);

    [HttpGet("payment-runs")]
    public Task<IReadOnlyList<PaymentRunDto>> PaymentRuns(CancellationToken ct) => _claims.ListPaymentRunsAsync(ct);
}
