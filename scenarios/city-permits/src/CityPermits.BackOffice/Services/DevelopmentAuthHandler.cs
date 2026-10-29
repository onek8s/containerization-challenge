using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CityPermits.BackOffice.Services;

/// <summary>Developer workstations only: signs in as the user in the dev-user cookie (default j.hart, supervisor).</summary>
public sealed class DevelopmentAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public new const string Scheme = "Development";

    public DevelopmentAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var user = Request.Cookies["dev-user"] ?? "CORP\\j.hart";
        var roles = Request.Cookies["dev-roles"] ?? "CaseOfficer,Supervisor,Finance,Administrator";
        var claims = new List<Claim> { new(ClaimTypes.Name, user) };
        claims.AddRange(roles.Split(',').Select(r => new Claim(ClaimTypes.Role, r)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme)));
    }
}
