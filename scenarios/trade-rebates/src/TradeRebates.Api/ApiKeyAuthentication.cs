using System.Security.Claims;
using System.Text.Encodings.Web;
using ContosoFoods.TradeRebates.Core;
using ContosoFoods.TradeRebates.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ContosoFoods.TradeRebates.Api;

/// <summary>Distributors authenticate with an X-Api-Key header. Each key belongs to one distributor.</summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IDistributorApiStore store) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    public const string DistributorIdClaim = "distributor_id";
    public const string DistributorCodeClaim = "distributor_code";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values))
            return AuthenticateResult.NoResult();

        string key = values.ToString();
        if (!ApiKeys.LooksValid(key))
            return AuthenticateResult.Fail("Malformed API key.");

        ApiClientInfo? client = await store.ResolveApiClientAsync(ApiKeys.Hash(key));
        if (client is null)
            return AuthenticateResult.Fail("Unknown or inactive API key.");

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "api:" + client.DistributorCode),
            new Claim(DistributorIdClaim, client.DistributorId.ToString()),
            new Claim(DistributorCodeClaim, client.DistributorCode),
            new Claim("api_client_id", client.ApiClientId.ToString())
        ], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}

public static class DistributorUser
{
    public static int DistributorId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ApiKeyAuthenticationHandler.DistributorIdClaim) ?? throw new InvalidOperationException("No distributor."));

    public static string DistributorCode(this ClaimsPrincipal user) =>
        user.FindFirstValue(ApiKeyAuthenticationHandler.DistributorCodeClaim) ?? "";
}
