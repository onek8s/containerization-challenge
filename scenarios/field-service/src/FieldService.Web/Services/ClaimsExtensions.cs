using System.Security.Claims;

namespace FieldService.Web.Services;

public static class ClaimsExtensions
{
    public const string TechnicianClaim = "technician_id";

    public static int? TechnicianId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(TechnicianClaim), out var id) ? id : null;

    public static string DisplayName(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.GivenName) ?? user.Identity?.Name ?? "";
}
