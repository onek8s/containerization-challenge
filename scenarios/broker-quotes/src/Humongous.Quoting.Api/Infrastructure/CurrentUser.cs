using System.Security.Claims;

namespace Humongous.Quoting.Api.Infrastructure;

public static class Policies
{
    public const string Broker = "Broker";
    public const string Underwriter = "Underwriter";
    public const string SeniorUnderwriter = "SeniorUnderwriter";
    public const string AnyUser = "AnyUser";
}

public static class ClaimsPrincipalExtensions
{
    public static string? AgencyCode(this ClaimsPrincipal user) =>
        user.FindFirst("agency")?.Value is { Length: > 0 } agency ? agency : null;

    public static bool IsUnderwriter(this ClaimsPrincipal user) =>
        user.IsInRole("Underwriter") || user.IsInRole("SeniorUnderwriter");

    public static string DisplayName(this ClaimsPrincipal user) =>
        user.FindFirst("name")?.Value ?? user.FindFirst("email")?.Value ?? user.FindFirst("sub")?.Value ?? "unknown";

    /// <summary>
    /// Agency filter for queries: underwriters see every agency, brokers only their own.
    /// Returns false when the user is neither.
    /// </summary>
    public static bool TryGetAgencyScope(this ClaimsPrincipal user, out string? agencyCode)
    {
        if (user.IsUnderwriter())
        {
            agencyCode = null;
            return true;
        }
        agencyCode = user.AgencyCode();
        return agencyCode is not null;
    }

    public static bool CanSee(this ClaimsPrincipal user, string agencyCode) =>
        user.IsUnderwriter() || string.Equals(user.AgencyCode(), agencyCode, StringComparison.OrdinalIgnoreCase);
}
