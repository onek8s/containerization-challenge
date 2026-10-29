using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Fabrikam.Housing.Identity.Data
{
    public class AuthDbContext : IdentityDbContext<ApplicationUser>
    {
        public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            if (Database.IsSqlServer()) builder.HasDefaultSchema("auth");

            builder.Entity<ApplicationUser>(e =>
            {
                e.Property(u => u.FullName).HasMaxLength(200);
                e.Property(u => u.TenancyReference).HasMaxLength(12);
                e.Property(u => u.Patch).HasMaxLength(10);
                e.HasIndex(u => u.TenantId);
            });
        }
    }
}
