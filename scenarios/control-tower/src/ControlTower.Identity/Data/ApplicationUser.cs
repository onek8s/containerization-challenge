using Microsoft.AspNetCore.Identity;

namespace Proseware.ControlTower.Identity.Data;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = "";

    /// <summary>Set for customer portal users; id of ct.Customer in the Control Tower database.</summary>
    public int? CustomerId { get; set; }

    /// <summary>Proseware staff (booking, operations, administration).</summary>
    public bool IsStaff { get; set; }

    public bool Disabled { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? LastLoginUtc { get; set; }
}
