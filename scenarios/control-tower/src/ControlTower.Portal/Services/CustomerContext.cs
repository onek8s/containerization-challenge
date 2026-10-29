using System.Globalization;
using System.Security.Claims;

namespace Proseware.ControlTower.Portal.Services;

/// <summary>The customer the signed-in portal user belongs to (customer_id claim from the sign-in service).</summary>
public sealed class CustomerContext
{
    public const string CustomerIdClaim = "customer_id";

    private readonly IHttpContextAccessor _http;

    public CustomerContext(IHttpContextAccessor http) => _http = http;

    public int? CustomerId => GetCustomerId(_http.HttpContext?.User);

    public int RequiredCustomerId => CustomerId ?? throw new UnauthorizedAccessException("The user has no customer account.");

    public string UserName => _http.HttpContext?.User.Identity?.Name ?? "portal";

    public static int? GetCustomerId(ClaimsPrincipal? user) =>
        int.TryParse(user?.FindFirst(CustomerIdClaim)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;

    public static string GroupName(int customerId) => "customer-" + customerId.ToString(CultureInfo.InvariantCulture);
}
