using ExpenseClaims.Api.Authentication;
using ExpenseClaims.Api.Models;
using ExpenseClaims.Core.Payments;
using ExpenseClaims.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseClaims.Api.Controllers;

[ApiController]
[Route("api/me")]
public sealed class MeController : ControllerBase
{
    [HttpGet]
    public async Task<MeDto> Get([FromServices] ICurrentUser user, [FromServices] ExpenseClaimsDbContext db, CancellationToken ct)
    {
        var me = await user.GetEmployeeAsync(ct);
        var isApprover = me is not null && await db.Employees.AnyAsync(e => e.ManagerEmployeeNumber == me.EmployeeNumber && e.Active, ct);
        return new MeDto(
            user.Upn,
            me?.EmployeeNumber,
            me?.DisplayName ?? user.Upn,
            me?.CostCenter,
            me is not null && Iban.IsValid(me.Iban),
            isApprover,
            user.IsFinance,
            user.IsAdministrator);
    }
}
