using ExpenseClaims.Api.Models;
using ExpenseClaims.Api.Services;
using ExpenseClaims.Data.Entities;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseClaims.Api.Controllers;

[ApiController]
[Route("api/approvals")]
public sealed class ApprovalsController : ControllerBase
{
    private readonly ClaimService _claims;

    public ApprovalsController(ClaimService claims) => _claims = claims;

    /// <summary>Claims waiting for the signed-in user's decision.</summary>
    [HttpGet]
    public Task<IReadOnlyList<ClaimSummaryDto>> Pending(CancellationToken ct) => _claims.ListPendingApprovalsAsync(ct);

    [HttpPost("{id:int}/approve")]
    public Task<ClaimDto> Approve(int id, DecisionRequest request, CancellationToken ct) =>
        _claims.DecideAsync(id, ApprovalDecision.Approved, request, ct);

    [HttpPost("{id:int}/reject")]
    public Task<ClaimDto> Reject(int id, DecisionRequest request, CancellationToken ct) =>
        _claims.DecideAsync(id, ApprovalDecision.Rejected, request, ct);

    [HttpPost("{id:int}/return")]
    public Task<ClaimDto> Return(int id, DecisionRequest request, CancellationToken ct) =>
        _claims.DecideAsync(id, ApprovalDecision.Returned, request, ct);
}
