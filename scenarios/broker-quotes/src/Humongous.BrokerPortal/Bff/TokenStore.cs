using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Humongous.BrokerPortal.Bff;

/// <summary>
/// Access tokens stay on the server in the authentication cookie; the browser never sees them.
/// Refreshes the access token with the refresh token shortly before it expires.
/// </summary>
public sealed class TokenStore(
    IHttpClientFactory httpClients,
    IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
    ILogger<TokenStore> logger)
{
    private static readonly TimeSpan RefreshBefore = TimeSpan.FromMinutes(2);

    public async Task<string?> GetAccessTokenAsync(HttpContext context)
    {
        var result = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Properties is null)
            return null;

        var token = result.Properties.GetTokenValue("access_token");
        var expiresAt = result.Properties.GetTokenValue("expires_at");
        if (token is not null && expiresAt is not null
            && DateTimeOffset.Parse(expiresAt, CultureInfo.InvariantCulture) - RefreshBefore > DateTimeOffset.UtcNow)
        {
            return token;
        }

        var refreshToken = result.Properties.GetTokenValue("refresh_token");
        if (refreshToken is null)
            return null;

        var options = oidcOptions.Get(OpenIdConnectDefaults.AuthenticationScheme);
        var configuration = await options.ConfigurationManager!.GetConfigurationAsync(context.RequestAborted);
        var client = httpClients.CreateClient("oidc");
        using var response = await client.PostAsync(configuration.TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = options.ClientId!,
            ["client_secret"] = options.ClientSecret!
        }), context.RequestAborted);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Token refresh for {User} failed with {Status}", context.User.Identity?.Name, response.StatusCode);
            return null;
        }

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.RequestAborted));
        var root = json.RootElement;
        var newAccess = root.GetProperty(OpenIdConnectParameterNames.AccessToken).GetString()!;
        var expiresIn = root.GetProperty(OpenIdConnectParameterNames.ExpiresIn).GetInt32();
        var newRefresh = root.TryGetProperty(OpenIdConnectParameterNames.RefreshToken, out var r) ? r.GetString() : refreshToken;

        result.Properties.UpdateTokenValue("access_token", newAccess);
        result.Properties.UpdateTokenValue("refresh_token", newRefresh!);
        result.Properties.UpdateTokenValue("expires_at",
            DateTimeOffset.UtcNow.AddSeconds(expiresIn).ToString("o", CultureInfo.InvariantCulture));
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, result.Principal!, result.Properties);
        return newAccess;
    }
}
