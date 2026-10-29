using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ExpenseClaims.Api.Authentication;
using ExpenseClaims.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ExpenseClaims.Api.Development;

/// <summary>
/// Test sign-in used when <c>Authentication:Mode</c> is <c>Development</c>: issues access tokens for
/// employees in the database without ADFS. Returns 404 in any other mode.
/// </summary>
[ApiController]
[Route("dev")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class DevTokenController : ControllerBase
{
    private readonly ExpensesAuthenticationOptions _options;

    public DevTokenController(IOptions<ExpensesAuthenticationOptions> options) => _options = options.Value;

    public sealed record TokenRequest(string Upn);

    [HttpGet("users")]
    public async Task<IActionResult> Users([FromServices] ExpenseClaimsDbContext db, CancellationToken ct)
    {
        if (!_options.IsDevelopmentMode)
            return NotFound();
        var users = await db.Employees.AsNoTracking().Where(e => e.Active).OrderBy(e => e.DisplayName)
            .Select(e => new { e.Upn, e.DisplayName, e.EmployeeNumber, e.ManagerEmployeeNumber })
            .ToListAsync(ct);
        return Ok(users.Select(u => new
        {
            u.Upn,
            u.DisplayName,
            u.EmployeeNumber,
            u.ManagerEmployeeNumber,
            Roles = _options.Development.Roles.TryGetValue(u.Upn, out var roles) ? roles : Array.Empty<string>()
        }));
    }

    [HttpPost("token")]
    public async Task<IActionResult> Token(TokenRequest request, [FromServices] ExpenseClaimsDbContext db, CancellationToken ct)
    {
        if (!_options.IsDevelopmentMode)
            return NotFound();
        var upn = request.Upn.Trim().ToLowerInvariant();
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(e => e.Upn == upn && e.Active, ct);
        if (employee is null)
            return BadRequest(new { error = $"{upn} is not an active employee." });

        var dev = _options.Development;
        var claims = new List<Claim>
        {
            new(ExpenseClaimTypes.Upn, employee.Upn),
            new(ExpenseClaimTypes.Name, employee.DisplayName),
            new(ExpenseClaimTypes.Scope, "openid"),
            new(JwtRegisteredClaimNames.Sub, employee.EmployeeNumber)
        };
        if (dev.Roles.TryGetValue(employee.Upn, out var roles))
            claims.AddRange(roles.Select(r => new Claim(ExpenseClaimTypes.Role, r)));

        var expires = DateTime.UtcNow.AddMinutes(dev.TokenLifetimeMinutes);
        var token = new JwtSecurityToken(
            dev.Issuer,
            dev.Audience,
            claims,
            DateTime.UtcNow,
            expires,
            new SigningCredentials(AuthenticationSetup.DevelopmentSigningKey(dev), SecurityAlgorithms.HmacSha256));
        return Ok(new
        {
            access_token = new JwtSecurityTokenHandler().WriteToken(token),
            token_type = "Bearer",
            expires_in = dev.TokenLifetimeMinutes * 60
        });
    }
}
