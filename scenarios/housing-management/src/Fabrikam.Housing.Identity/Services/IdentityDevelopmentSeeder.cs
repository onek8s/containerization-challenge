using System;
using System.Linq;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Identity.Data;
using IdentityServer4.EntityFramework.DbContexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fabrikam.Housing.Identity.Services
{
    /// <summary>
    /// Development only: creates the identity and grant databases and portal accounts for the seeded
    /// tenants that have a PortalSubjectId (password Tenant123!).
    /// </summary>
    public static class IdentityDevelopmentSeeder
    {
        public const string TenantPassword = "Tenant123!";

        public static void Seed(IServiceProvider services)
        {
            using (var scope = services.CreateScope())
            {
                var sp = scope.ServiceProvider;
                var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(IdentityDevelopmentSeeder));
                sp.GetRequiredService<AuthDbContext>().Database.EnsureCreated();
                sp.GetRequiredService<PersistedGrantDbContext>().Database.EnsureCreated();

                var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
                var housing = sp.GetRequiredService<HousingDbContext>();
                var tenants = housing.Tenants.Include(t => t.Tenancy).Where(t => t.PortalSubjectId != null).ToList();
                foreach (var tenant in tenants)
                {
                    if (users.FindByIdAsync(tenant.PortalSubjectId).GetAwaiter().GetResult() != null) continue;
                    var email = tenant.Email ?? tenant.PortalSubjectId + "@example.com";
                    var user = new ApplicationUser
                    {
                        Id = tenant.PortalSubjectId,
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true,
                        FullName = tenant.FirstName + " " + tenant.LastName,
                        TenantId = tenant.Id,
                        TenancyReference = tenant.Tenancy.Reference,
                        CreatedAt = DateTime.Now
                    };
                    var result = users.CreateAsync(user, TenantPassword).GetAwaiter().GetResult();
                    if (result.Succeeded)
                        logger.LogInformation("Development portal account {Email} for tenancy {Tenancy}", email, user.TenancyReference);
                    else
                        logger.LogWarning("Could not create {Email}: {Errors}", email, string.Join("; ", result.Errors.Select(e => e.Description)));
                }
            }
        }
    }
}
