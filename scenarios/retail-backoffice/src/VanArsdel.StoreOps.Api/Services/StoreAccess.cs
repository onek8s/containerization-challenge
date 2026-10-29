using System;
using System.Security.Claims;
using VanArsdel.BuildingBlocks.Exceptions;
using VanArsdel.BuildingBlocks.Security;
using VanArsdel.Contracts;

namespace VanArsdel.StoreOps.Api.Services
{
    /// <summary>
    /// Store staff only see their own store (store_code claim from Entra ID, extension attribute from ADFS).
    /// Head office roles see every store.
    /// </summary>
    public static class StoreAccess
    {
        public static bool IsHeadOffice(ClaimsPrincipal user) =>
            user.HasAnyRole(Roles.Admin, Roles.Merchandiser, Roles.CustomerService, Roles.Finance, Roles.Service);

        public static string Resolve(ClaimsPrincipal user, string requested)
        {
            if (IsHeadOffice(user))
            {
                return string.IsNullOrWhiteSpace(requested) ? null : requested.ToUpperInvariant();
            }
            var own = user.StoreCode();
            if (string.IsNullOrEmpty(own))
            {
                throw new DomainException("The signed-in user has no store assigned.");
            }
            if (!string.IsNullOrWhiteSpace(requested) && !string.Equals(own, requested, StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainException("Access to store " + requested + " is not allowed.");
            }
            return own.ToUpperInvariant();
        }
    }
}
