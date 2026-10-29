using System.Security.Claims;
using System.Text.Encodings.Web;
using FurnaceRecords.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FurnaceRecords.Api;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    public const string Header = "X-Api-Key";

    private readonly ApiKeyService _keys;

    public ApiKeyAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder, ApiKeyService keys)
        : base(options, logger, encoder)
    {
        _keys = keys;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Header, out var values) || string.IsNullOrWhiteSpace(values.ToString()))
        {
            return AuthenticateResult.NoResult();
        }
        var client = await _keys.FindAsync(values.ToString(), Context.RequestAborted);
        if (client is null)
        {
            return AuthenticateResult.Fail("Unknown API key.");
        }
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, client.Name) }, SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
