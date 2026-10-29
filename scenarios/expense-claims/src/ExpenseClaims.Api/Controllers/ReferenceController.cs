using ExpenseClaims.Core.Claims;
using ExpenseClaims.Core.Policy;
using ExpenseClaims.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseClaims.Api.Controllers;

[ApiController]
[Route("api/reference")]
public sealed class ReferenceController : ControllerBase
{
    [HttpGet("categories")]
    public IEnumerable<object> Categories() =>
        Enum.GetValues<ExpenseCategory>().Select(c => new { id = (int)c, name = c.ToString() });

    [HttpGet("policy")]
    public ExpensePolicyOptions Policy([FromServices] ExpensePolicy policy) => policy.Options;

    /// <summary>Currencies that have a reference rate in the last two weeks.</summary>
    [HttpGet("currencies")]
    [ResponseCache(Duration = 3600)]
    public async Task<IEnumerable<string>> Currencies([FromServices] ExpenseClaimsDbContext db, CancellationToken ct)
    {
        var from = DateOnly.FromDateTime(DateTime.Today.AddDays(-14));
        var currencies = await db.ExchangeRates.Where(r => r.Date >= from).Select(r => r.Currency).Distinct().ToListAsync(ct);
        return currencies.Append("EUR").Distinct().OrderBy(c => c);
    }
}
