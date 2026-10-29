using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VanArsdel.IntegrationTests;

/// <summary>
/// Stands in for Entra ID / ADFS. "X-Test-User: fi012.staff; roles=Retail.StoreStaff; store=FI012".
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string Header = "X-Test-User";

    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var value = Request.Headers[Header].ToString();
        if (string.IsNullOrEmpty(value)) return Task.FromResult(AuthenticateResult.NoResult());

        var parts = value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var claims = new List<Claim> { new("preferred_username", parts[0]), new("name", parts[0]) };
        foreach (var part in parts.Skip(1))
        {
            var kv = part.Split('=', 2);
            switch (kv[0])
            {
                case "roles":
                    claims.AddRange(kv[1].Split(',').Select(r => new Claim("roles", r)));
                    break;
                case "store":
                    claims.Add(new Claim("store_code", kv[1]));
                    break;
            }
        }
        var identity = new ClaimsIdentity(claims, SchemeName, "name", "roles");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
