using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Litware.OrderHub.Portal.Services
{
    /// <summary>Identity tables in the OrderHub database (created by db\01-Schema.sql).</summary>
    public class PortalIdentityContext : IdentityDbContext<IdentityUser, IdentityRole, string>
    {
        public PortalIdentityContext(DbContextOptions<PortalIdentityContext> options) : base(options)
        {
        }
    }

    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Operator = "Operator";
        public static readonly string[] All = { Admin, Operator };
    }
}
