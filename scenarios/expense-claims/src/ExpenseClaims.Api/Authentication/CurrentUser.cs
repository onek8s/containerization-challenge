using System.Security.Claims;
using ExpenseClaims.Data;
using ExpenseClaims.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseClaims.Api.Authentication;

public interface ICurrentUser
{
    string Upn { get; }
    bool IsFinance { get; }
    bool IsAdministrator { get; }

    /// <summary>The employee record of the signed-in user, or null when the HR import has not created one yet.</summary>
    Task<Employee?> GetEmployeeAsync(CancellationToken cancellationToken = default);
}

public sealed class CurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal _principal;
    private readonly ExpenseClaimsDbContext _db;
    private Employee? _employee;
    private bool _loaded;

    public CurrentUser(IHttpContextAccessor accessor, ExpenseClaimsDbContext db)
    {
        _principal = accessor.HttpContext?.User ?? new ClaimsPrincipal();
        _db = db;
    }

    public string Upn => _principal.FindFirstValue(ExpenseClaimTypes.Upn) ?? "";
    public bool IsFinance => _principal.IsInRole(Roles.Finance) || IsAdministrator;
    public bool IsAdministrator => _principal.IsInRole(Roles.Administrator);

    public async Task<Employee?> GetEmployeeAsync(CancellationToken cancellationToken = default)
    {
        if (!_loaded)
        {
            var upn = Upn.ToLowerInvariant();
            _employee = await _db.Employees.SingleOrDefaultAsync(e => e.Upn == upn && e.Active, cancellationToken);
            _loaded = true;
        }
        return _employee;
    }
}
