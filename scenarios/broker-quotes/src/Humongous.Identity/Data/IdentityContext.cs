using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Humongous.Identity.Data;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = "";

    /// <summary>Broker agency of a broker user; empty for Humongous staff.</summary>
    public string? AgencyCode { get; set; }

    public DateTime? LastSignInUtc { get; set; }
}

public class IdentityContext(DbContextOptions<IdentityContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.DisplayName).HasMaxLength(100);
            e.Property(u => u.AgencyCode).HasMaxLength(10);
        });
        builder.UseOpenIddict();
    }
}

public static class Roles
{
    public const string Broker = "Broker";
    public const string BrokerAdmin = "BrokerAdmin";
    public const string Underwriter = "Underwriter";
    public const string SeniorUnderwriter = "SeniorUnderwriter";
    public const string Administrator = "Administrator";

    public static readonly string[] All = [Broker, BrokerAdmin, Underwriter, SeniorUnderwriter, Administrator];
}

public static class CustomClaims
{
    public const string Agency = "agency";
    public const string Scope = "quoting_api";
    public const string ApiResource = "quote-api";
}
