using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Proseware.ControlTower.Tower.Auth;

/// <summary>
/// Development only: signs every request in as a fixed operations user so the SPA can be
/// used without the token service. The user name can be overridden with the X-Dev-User header.
/// </summary>
public sealed class DevelopmentAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Development";

    public DevelopmentAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var user = Request.Headers.TryGetValue("X-Dev-User", out var h) && !string.IsNullOrWhiteSpace(h) ? h.ToString() : "dev.operator";
        var claims = new List<Claim>
        {
            new("sub", user),
            new("name", user),
            new("role", "ops"),
            new("role", "admin")
        };
        var identity = new ClaimsIdentity(claims, SchemeName, "name", "role");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
