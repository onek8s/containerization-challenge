using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Litware.DocControl.Web.Security;

public sealed class DevelopmentAuthenticationOptions : AuthenticationSchemeOptions
{
    public string UserName { get; set; } = @"CORP\developer";
    public List<string> Roles { get; set; } = new();
}

/// <summary>Signs every request in as the configured user. Only used with Authentication:Mode = Development.</summary>
public sealed class DevelopmentAuthenticationHandler : AuthenticationHandler<DevelopmentAuthenticationOptions>
{
    public const string SchemeName = "Development";

    public DevelopmentAuthenticationHandler(IOptionsMonitor<DevelopmentAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userName = Request.Headers.TryGetValue("X-Dev-User", out var u) && !string.IsNullOrEmpty(u) ? u.ToString() : Options.UserName;
        var claims = new List<Claim> { new(ClaimTypes.Name, userName) };
        claims.AddRange(Options.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
