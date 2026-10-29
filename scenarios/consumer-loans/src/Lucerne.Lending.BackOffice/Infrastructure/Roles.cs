using System.Security.Claims;

namespace Lucerne.Lending.BackOffice.Infrastructure;

/// <summary>AD groups sent by ADFS as role claims (relying party "Lucerne Lending Back Office").</summary>
public static class Roles
{
    public const string Underwriter = "LND-Underwriters";
    public const string SeniorUnderwriter = "LND-SeniorUnderwriters";
    public const string Operations = "LND-Operations";
    public const string Collections = "LND-Collections";
    public const string ReadOnly = "LND-ReadOnly";

    public static readonly string[] All = { Underwriter, SeniorUnderwriter, Operations, Collections, ReadOnly };

    public static string UserName(this ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.Upn)?.Value ?? user.FindFirst(ClaimTypes.WindowsAccountName)?.Value ?? user.Identity?.Name ?? "unknown";

    public static string DisplayName(this ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.GivenName) is { } g && user.FindFirst(ClaimTypes.Surname) is { } s ? $"{g.Value} {s.Value}" : user.UserName();
}
