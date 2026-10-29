using System.Security.Claims;
using System.Text.Encodings.Web;
using FreightRates.Data.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FreightRates.Api.Infrastructure;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    public const string CustomerIdClaim = "customer_id";

    private readonly CustomerService _customers;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        CustomerService customers)
        : base(options, logger, encoder)
    {
        _customers = customers;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values) || string.IsNullOrWhiteSpace(values))
            return AuthenticateResult.NoResult();

        var customer = await _customers.FindByApiKeyAsync(values.ToString(), Context.RequestAborted);
        if (customer is null)
            return AuthenticateResult.Fail("Invalid API key.");

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, customer.Code),
            new Claim(CustomerIdClaim, customer.Id.ToString()),
        }, SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}

public static class ClaimsPrincipalExtensions
{
    public static int CustomerId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ApiKeyAuthenticationHandler.CustomerIdClaim)!);
}
