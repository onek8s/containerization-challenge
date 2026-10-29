using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace Humongous.Workbench.Services;

/// <summary>Tokens of the signed-in user, handed from the first HTTP request to the circuit.</summary>
public sealed class InitialTokens
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? ExpiresAt { get; set; }
}

/// <summary>
/// Circuit-scoped holder of the user's access token. The circuit outlives the HTTP request, so the
/// token is refreshed here with the refresh token instead of through the cookie.
/// </summary>
public sealed class UserTokens(
    IHttpClientFactory httpClients,
    IOptionsMonitor<OpenIdConnectOptions> oidc,
    ILogger<UserTokens> logger)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _access;
    private string? _refresh;
    private DateTimeOffset _expires;

    public bool HasTokens => _access is not null;

    public void Initialise(InitialTokens tokens)
    {
        if (_access is not null || tokens.AccessToken is null) return;
        _access = tokens.AccessToken;
        _refresh = tokens.RefreshToken;
        _expires = tokens.ExpiresAt is null ? DateTimeOffset.UtcNow.AddMinutes(5) : DateTimeOffset.Parse(tokens.ExpiresAt, CultureInfo.InvariantCulture);
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        if (_access is null) return null;
        if (_expires - TimeSpan.FromMinutes(2) > DateTimeOffset.UtcNow) return _access;

        await _lock.WaitAsync(ct);
        try
        {
            if (_expires - TimeSpan.FromMinutes(2) > DateTimeOffset.UtcNow || _refresh is null) return _access;
            var options = oidc.Get(OpenIdConnectDefaults.AuthenticationScheme);
            var configuration = await options.ConfigurationManager!.GetConfigurationAsync(ct);
            using var response = await httpClients.CreateClient("oidc").PostAsync(configuration.TokenEndpoint,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = _refresh,
                    ["client_id"] = options.ClientId!,
                    ["client_secret"] = options.ClientSecret!
                }), ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Refreshing the workbench token failed: {Status}", response.StatusCode);
                return _access;
            }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            _access = json.RootElement.GetProperty("access_token").GetString();
            if (json.RootElement.TryGetProperty("refresh_token", out var refresh)) _refresh = refresh.GetString();
            _expires = DateTimeOffset.UtcNow.AddSeconds(json.RootElement.GetProperty("expires_in").GetInt32());
            return _access;
        }
        finally
        {
            _lock.Release();
        }
    }
}
