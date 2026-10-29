using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Proseware.ControlTower.Scanning.Auth;

public sealed class ApiKeyOptions : AuthenticationSchemeOptions
{
    public const string Scheme = "DeviceKey";
    public const string HeaderName = "X-Device-Key";

    /// <summary>Device id -> key. Only for the TC51 handhelds that cannot do the client credentials flow.</summary>
    public Dictionary<string, string> Devices { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Older Zebra TC51 handhelds run the 2019 scanning app that sends a static device key
/// ("X-Device-Key: &lt;device&gt;:&lt;key&gt;") instead of a bearer token.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyOptions>
{
    public ApiKeyAuthenticationHandler(IOptionsMonitor<ApiKeyOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyOptions.HeaderName, out var values))
            return Task.FromResult(AuthenticateResult.NoResult());

        var value = values.ToString();
        var separator = value.IndexOf(':');
        if (separator <= 0)
            return Task.FromResult(AuthenticateResult.Fail("Malformed device key."));

        var device = value[..separator];
        var key = value[(separator + 1)..];
        if (!Options.Devices.TryGetValue(device, out var expected) || !FixedTimeEquals(expected, key))
        {
            Logger.LogWarning("Rejected device key for {Device} from {Address}", device, Context.Connection.RemoteIpAddress);
            return Task.FromResult(AuthenticateResult.Fail("Unknown device or key."));
        }

        var identity = new ClaimsIdentity(new[]
        {
            new Claim("client_id", "device:" + device),
            new Claim("device_id", device),
            new Claim("scope", "scan.api"),
            new Claim(ClaimTypes.Name, device)
        }, ApiKeyOptions.Scheme);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }

    private static bool FixedTimeEquals(string a, string b) =>
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(a), System.Text.Encoding.UTF8.GetBytes(b));
}
