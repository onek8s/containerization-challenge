using System.Globalization;
using System.Security.Claims;
using System.Security.Principal;
using Wingtip.FieldSales.Data;

namespace Wingtip.FieldSales.Api.Infrastructure
{
    public static class ClaimsExtensions
    {
        public static string RepCode(this IPrincipal user)
        {
            var claim = (user as ClaimsPrincipal)?.FindFirst(FieldSalesClaims.RepCode);
            return claim == null ? "" : claim.Value;
        }

        public static decimal DiscountLimit(this IPrincipal user)
        {
            var claim = (user as ClaimsPrincipal)?.FindFirst(FieldSalesClaims.DiscountLimit);
            decimal value;
            return claim != null && decimal.TryParse(claim.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out value) ? value : 0m;
        }

        public static string DisplayName(this IPrincipal user)
        {
            var claim = (user as ClaimsPrincipal)?.FindFirst(FieldSalesClaims.DisplayName);
            return claim == null ? user.Identity.Name : claim.Value;
        }
    }
}
