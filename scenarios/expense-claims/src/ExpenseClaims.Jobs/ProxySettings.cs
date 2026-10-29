using System.Net;

namespace ExpenseClaims.Jobs;

/// <summary>Outbound HTTP goes through the corporate proxy when <c>Proxy:Address</c> is set.</summary>
public static class ProxySettings
{
    public static HttpClientHandler CreateHandler(IConfiguration configuration)
    {
        var handler = new HttpClientHandler();
        var address = configuration["Proxy:Address"];
        if (!string.IsNullOrEmpty(address))
        {
            handler.Proxy = new WebProxy(address)
            {
                BypassProxyOnLocal = true,
                BypassList = configuration.GetSection("Proxy:Bypass").Get<string[]>() ?? Array.Empty<string>(),
                UseDefaultCredentials = configuration.GetValue("Proxy:UseDefaultCredentials", true)
            };
            handler.UseProxy = true;
        }
        return handler;
    }
}
