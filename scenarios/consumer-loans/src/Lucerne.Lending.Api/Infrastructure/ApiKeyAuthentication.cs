using System.Security.Claims;
using System.Text.Encodings.Web;
using Lucerne.Lending.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Lucerne.Lending.Api.Infrastructure;

public static class ApiScopes
{
    public const string Portal = "portal";
    public const string Office = "office";
    public const string Partner = "partner";
}

/// <summary>Calling applications authenticate with X-Api-Key (SHA-256 hashes in lending.ApiClient).</summary>
public sealed class ApiKeyHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    private readonly ApiClientRepository _clients;

    public ApiKeyHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock,
        ApiClientRepository clients) : base(options, logger, encoder, clock) => _clients = clients;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var key) || string.IsNullOrWhiteSpace(key)) return AuthenticateResult.NoResult();
        var client = await _clients.FindAsync(key!);
        if (client == null) return AuthenticateResult.Fail("Unknown API key.");
        var claims = new List<Claim> { new(ClaimTypes.Name, client.Name), new("client", client.Name) };
        claims.AddRange(client.Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(s => new Claim("scope", s)));
        // the calling application passes the end user (portal customer id or office user) for the audit trail
        if (Request.Headers.TryGetValue("X-On-Behalf-Of", out var user) && !string.IsNullOrWhiteSpace(user))
            claims.Add(new Claim("on_behalf_of", user!));
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name));
    }
}

public static class PrincipalExtensions
{
    public static string Actor(this ClaimsPrincipal user) =>
        user.FindFirst("on_behalf_of")?.Value is { } who ? $"{user.Identity?.Name}:{who}" : user.Identity?.Name ?? "anonymous";

    public static string? OnBehalfOf(this ClaimsPrincipal user) => user.FindFirst("on_behalf_of")?.Value;
}
