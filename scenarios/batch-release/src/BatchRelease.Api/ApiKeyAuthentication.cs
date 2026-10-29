using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using BatchRelease.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BatchRelease.Api;

/// <summary>
/// API keys look like <c>br_1a2b3c4d_secret</c>. The 8-character prefix finds the client;
/// the SHA-256 of the whole key is compared with the stored hash.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    public const string SubmitClaim = "br:submit";
    public const string ReadClaim = "br:read";
    public const string SiteClaim = "br:site";

    private readonly BatchReleaseContext _db;

    public ApiKeyAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, BatchReleaseContext db)
        : base(options, logger, encoder) => _db = db;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values))
            return AuthenticateResult.NoResult();

        var key = values.ToString();
        var parts = key.Split('_', 3);
        if (parts.Length != 3 || parts[0] != "br" || parts[1].Length != 8)
            return AuthenticateResult.Fail("Malformed API key.");

        var hash = Hash(key);
        var client = await _db.ApiClients.FirstOrDefaultAsync(c => c.KeyPrefix == parts[1] && c.Active, Context.RequestAborted);
        if (client is null || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(client.KeyHash.Trim()), Encoding.ASCII.GetBytes(hash)))
            return AuthenticateResult.Fail("Unknown API key.");

        client.LastUsedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(Context.RequestAborted);

        var claims = new List<Claim> { new(ClaimTypes.Name, client.Name) };
        if (client.CanSubmit)
            claims.Add(new Claim(SubmitClaim, "true"));
        if (client.CanRead)
            claims.Add(new Claim(ReadClaim, "true"));
        if (client.SiteCode is not null)
            claims.Add(new Claim(SiteClaim, client.SiteCode));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    public static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
}
