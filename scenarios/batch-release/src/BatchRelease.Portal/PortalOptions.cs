using System.Security.Claims;

namespace BatchRelease.Portal;

public sealed class PortalOptions
{
    public const string Section = "Portal";

    public string SiteName { get; set; } = "Batch Release";
    public int QueueSize { get; set; } = 200;
}

public static class UserExtensions
{
    /// <summary>SID of the signed-in Windows user.</summary>
    public static string Sid(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.PrimarySid)
        ?? throw new InvalidOperationException("The signed-in user has no Windows SID.");
}
