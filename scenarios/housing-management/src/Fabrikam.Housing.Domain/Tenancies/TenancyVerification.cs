using System;
using Fabrikam.Housing.Common;

namespace Fabrikam.Housing.Tenancies
{
    /// <summary>
    /// Checks the details a tenant gives when registering for the portal.
    /// </summary>
    public static class TenancyVerification
    {
        public static bool Matches(Tenant tenant, string tenancyRef, DateTime dateOfBirth, string postcode)
        {
            if (tenant?.Tenancy?.Property == null) return false;
            if (tenant.Tenancy.Status == TenancyStatus.Ended) return false;
            if (!string.Equals(tenant.Tenancy.Reference, (tenancyRef ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) return false;
            if (tenant.DateOfBirth.Date != dateOfBirth.Date) return false;
            return Postcode.TryNormalise(postcode, out var given)
                && Postcode.TryNormalise(tenant.Tenancy.Property.Postcode, out var actual)
                && given == actual;
        }
    }
}
