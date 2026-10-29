using ExpenseClaims.Api.Models;
using ExpenseClaims.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseClaims.Api.Controllers;

[ApiController]
[Route("api/card-transactions")]
public sealed class CardTransactionsController : ControllerBase
{
    private readonly ClaimService _claims;

    public CardTransactionsController(ClaimService claims) => _claims = claims;

    /// <summary>Company card transactions of the signed-in employee that are not on any claim yet.</summary>
    [HttpGet]
    public Task<IReadOnlyList<CardTransactionDto>> Open(CancellationToken ct) => _claims.ListOpenCardTransactionsAsync(ct);
}
