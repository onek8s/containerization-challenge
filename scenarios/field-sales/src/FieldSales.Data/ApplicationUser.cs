using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace Wingtip.FieldSales.Data
{
    public static class Roles
    {
        public const string SalesRep = "SalesRep";
        public const string SalesManager = "SalesManager";
        public const string Administrator = "Administrator";
    }

    public static class FieldSalesClaims
    {
        public const string RepCode = "http://schemas.wingtipfoods.com/fieldsales/repcode";
        public const string DiscountLimit = "http://schemas.wingtipfoods.com/fieldsales/discountlimit";
        public const string DisplayName = "http://schemas.wingtipfoods.com/fieldsales/displayname";
    }

    public class ApplicationUser : IdentityUser
    {
        public string DisplayName { get; set; }

        /// <summary>Two to four letters, used in rep references and customer assignment. Empty for office users.</summary>
        public string RepCode { get; set; }

        public decimal DiscountLimitPercent { get; set; }

        public async Task<ClaimsIdentity> CreateIdentityAsync(UserManager<ApplicationUser> manager, string authenticationType)
        {
            var identity = await manager.CreateIdentityAsync(this, authenticationType);
            identity.AddClaim(new Claim(FieldSalesClaims.DisplayName, DisplayName ?? UserName));
            identity.AddClaim(new Claim(FieldSalesClaims.RepCode, RepCode ?? ""));
            identity.AddClaim(new Claim(FieldSalesClaims.DiscountLimit,
                DiscountLimitPercent.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            return identity;
        }
    }
}
