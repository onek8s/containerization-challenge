using ExpenseClaims.Api.Authentication;
using ExpenseClaims.Data;
using ExpenseClaims.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseClaims.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = Policies.Administrator)]
public sealed class AdminController : ControllerBase
{
    private readonly ExpenseClaimsDbContext _db;

    public AdminController(ExpenseClaimsDbContext db) => _db = db;

    public sealed record CardDto(int Id, string MaskedNumber, string EmployeeNumber, string Employee, bool Active);

    public sealed record LinkCardRequest(string MaskedNumber, string EmployeeNumber);

    [HttpGet("cards")]
    public async Task<IEnumerable<CardDto>> Cards(CancellationToken ct) =>
        (await _db.CompanyCards.AsNoTracking().Include(c => c.Employee).OrderBy(c => c.Employee.DisplayName).ToListAsync(ct))
            .Select(c => new CardDto(c.Id, c.MaskedNumber, c.Employee.EmployeeNumber, c.Employee.DisplayName, c.Active));

    /// <summary>Links a company card (masked number as in the card company's file) to an employee.</summary>
    [HttpPost("cards")]
    public async Task<ActionResult<CardDto>> LinkCard(LinkCardRequest request, CancellationToken ct)
    {
        var employee = await _db.Employees.SingleOrDefaultAsync(e => e.EmployeeNumber == request.EmployeeNumber, ct);
        if (employee is null)
            return Problem(title: $"Employee {request.EmployeeNumber} was not found.", statusCode: StatusCodes.Status422UnprocessableEntity);
        var masked = request.MaskedNumber.Replace(" ", "");
        var card = await _db.CompanyCards.SingleOrDefaultAsync(c => c.MaskedNumber == masked, ct);
        if (card is null)
        {
            card = new CompanyCard { MaskedNumber = masked };
            _db.CompanyCards.Add(card);
        }
        card.Employee = employee;
        card.Active = true;
        await _db.SaveChangesAsync(ct);
        return new CardDto(card.Id, card.MaskedNumber, employee.EmployeeNumber, employee.DisplayName, card.Active);
    }

    [HttpDelete("cards/{id:int}")]
    public async Task<IActionResult> DeactivateCard(int id, CancellationToken ct)
    {
        var card = await _db.CompanyCards.FindAsync(new object[] { id }, ct);
        if (card is null)
            return NotFound();
        card.Active = false;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Employee register as imported from HR, for checking line managers and bank accounts.</summary>
    [HttpGet("employees")]
    public async Task<IEnumerable<object>> Employees([FromQuery] string? search, CancellationToken ct)
    {
        var query = _db.Employees.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(e => e.DisplayName.Contains(search) || e.EmployeeNumber == search || e.Upn.Contains(search));
        return await query.OrderBy(e => e.DisplayName).Take(200)
            .Select(e => new { e.EmployeeNumber, e.DisplayName, e.Upn, e.CostCenter, e.ManagerEmployeeNumber, HasBankAccount = e.Iban != null, e.Active, e.UpdatedAt })
            .ToListAsync(ct);
    }
}
