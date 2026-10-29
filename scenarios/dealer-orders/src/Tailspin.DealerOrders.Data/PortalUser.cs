using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace Tailspin.DealerOrders.Data
{
    /// <summary>Dealer portal user (ASP.NET Identity 2). Every user belongs to exactly one dealer.</summary>
    public class PortalUser : IdentityUser
    {
        public int DealerId { get; set; }
        public string DisplayName { get; set; }
        public string LanguageCode { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? LastLoginUtc { get; set; }
        public bool Disabled { get; set; }

        public const string DealerIdClaim = "http://schemas.tailspin-machinery.example/claims/dealerid";
        public const string DisplayNameClaim = "http://schemas.tailspin-machinery.example/claims/displayname";

        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<PortalUser> manager, string authenticationType)
        {
            var identity = await manager.CreateIdentityAsync(this, authenticationType);
            identity.AddClaim(new Claim(DealerIdClaim, DealerId.ToString()));
            identity.AddClaim(new Claim(DisplayNameClaim, DisplayName ?? UserName));
            return identity;
        }
    }

    public static class PortalRoles
    {
        /// <summary>Manages the dealer's own portal users and sees all of the dealer's documents.</summary>
        public const string DealerAdmin = "DealerAdmin";

        /// <summary>Configures machines, makes quotes and places machine orders.</summary>
        public const string DealerSales = "DealerSales";

        /// <summary>Orders spare parts and checks stock.</summary>
        public const string DealerParts = "DealerParts";

        public static readonly string[] All = { DealerAdmin, DealerSales, DealerParts };
    }
}
