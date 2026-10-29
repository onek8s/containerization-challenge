using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Lucerne.Lending.CustomerPortal.Identity;

public sealed class PortalUser : IdentityUser
{
    /// <summary>Personal identity code given at registration; links the account to applications and core customer data.</summary>
    public string PersonalIdentityCode { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateTime RegisteredUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSignInUtc { get; set; }
}

public sealed class PortalIdentityDbContext : IdentityDbContext<PortalUser>
{
    public PortalIdentityDbContext(DbContextOptions<PortalIdentityDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        if (Database.IsSqlServer()) builder.HasDefaultSchema("portal");
        builder.Entity<PortalUser>(e =>
        {
            e.Property(u => u.PersonalIdentityCode).HasMaxLength(11).IsRequired();
            e.Property(u => u.FirstName).HasMaxLength(100);
            e.Property(u => u.LastName).HasMaxLength(100);
            e.HasIndex(u => u.PersonalIdentityCode).IsUnique();
        });
    }
}

public static class PortalClaims
{
    public const string PersonalIdentityCode = "lucerne:pic";
    public const string FullName = "lucerne:name";
}

/// <summary>Adds the identity code and name to the cookie so the API calls do not need a database round trip.</summary>
public sealed class PortalClaimsFactory : UserClaimsPrincipalFactory<PortalUser, IdentityRole>
{
    public PortalClaimsFactory(UserManager<PortalUser> users, RoleManager<IdentityRole> roles, Microsoft.Extensions.Options.IOptions<IdentityOptions> options)
        : base(users, roles, options) { }

    protected override async Task<System.Security.Claims.ClaimsIdentity> GenerateClaimsAsync(PortalUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new(PortalClaims.PersonalIdentityCode, user.PersonalIdentityCode));
        identity.AddClaim(new(PortalClaims.FullName, $"{user.FirstName} {user.LastName}".Trim()));
        return identity;
    }
}
