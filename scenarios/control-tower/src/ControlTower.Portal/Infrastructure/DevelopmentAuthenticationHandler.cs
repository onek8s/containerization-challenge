using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

using Proseware.ControlTower.Portal.Services;

namespace Proseware.ControlTower.Portal.Infrastructure;

/// <summary>
/// Development only: signs every request in as the configured user so the application can be used
/// without the sign-in service (Authentication:DevelopmentUser / DevelopmentRoles).
/// </summary>
public sealed class DevelopmentAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string Scheme = "Development";
    private readonly IConfiguration _configuration;

    public DevelopmentAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
        ISystemClock clock, IConfiguration configuration)
        : base(options, logger, encoder, clock) => _configuration = configuration;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var user = _configuration["Authentication:DevelopmentUser"] ?? "developer";
        var roles = (_configuration["Authentication:DevelopmentRoles"] ?? "customer").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var claims = new List<Claim> { new("name", user), new("sub", "dev-" + user) };
        claims.AddRange(roles.Select(r => new Claim("role", r)));
        if (_configuration["Authentication:DevelopmentCustomerId"] is { Length: > 0 } customerId)
            claims.Add(new Claim(CustomerContext.CustomerIdClaim, customerId));
        var identity = new ClaimsIdentity(claims, Scheme, "name", "role");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
    }
}
