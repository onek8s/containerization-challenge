using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FurnaceRecords.Portal.Services;

/// <summary>Signs every request in as the configured development user.</summary>
public sealed class DevelopmentAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Development";

    private readonly PortalOptions _portal;

    public DevelopmentAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder, PortalOptions portal)
        : base(options, logger, encoder)
    {
        _portal = portal;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // ?as=CORP\user&groups=CORP\FR-Operators signs in as another user with the given groups.
        var user = _portal.DevelopmentUser;
        IEnumerable<string> groups = _portal.DevelopmentGroups;
        if (Request.Query.TryGetValue("as", out var a))
        {
            user = a.ToString();
            groups = Request.Query["groups"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        var claims = new List<Claim> { new(ClaimTypes.Name, user) };
        claims.AddRange(groups.Select(g => new Claim(ClaimTypes.Role, g)));
        var identity = new ClaimsIdentity(claims, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
