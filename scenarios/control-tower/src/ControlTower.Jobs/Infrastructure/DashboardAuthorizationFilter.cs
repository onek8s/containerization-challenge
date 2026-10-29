using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Hangfire.Dashboard;

namespace Proseware.ControlTower.Jobs.Infrastructure;

/// <summary>Local requests are allowed; others need the operations basic credentials.</summary>
public sealed class DashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly DashboardOptions _options;

    public DashboardAuthorizationFilter(DashboardOptions options) => _options = options;

    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        var remote = http.Connection.RemoteIpAddress;
        if (remote is not null && (IPAddress.IsLoopback(remote) || remote.Equals(http.Connection.LocalIpAddress)))
            return true;

        if (string.IsNullOrEmpty(_options.Password)) return Challenge(http);

        var header = http.Request.Headers.Authorization.ToString();
        if (!AuthenticationHeaderValue.TryParse(header, out var auth) || !string.Equals(auth.Scheme, "Basic", StringComparison.OrdinalIgnoreCase) || auth.Parameter is null)
            return Challenge(http);

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(auth.Parameter));
        }
        catch (FormatException)
        {
            return Challenge(http);
        }

        var separator = decoded.IndexOf(':');
        if (separator < 0) return Challenge(http);
        var user = decoded[..separator];
        var password = decoded[(separator + 1)..];
        var ok = string.Equals(user, _options.User, StringComparison.OrdinalIgnoreCase)
                 && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(_options.Password));
        return ok || Challenge(http);
    }

    private static bool Challenge(HttpContext http)
    {
        http.Response.Headers.WWWAuthenticate = "Basic realm=\"Control Tower jobs\"";
        http.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return false;
    }
}
