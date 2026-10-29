namespace Fabrikam.Housing.Hosting.Security
{
    /// <summary>
    /// Staff roles. The identity server issues them as "role" claims from the AD groups
    /// HSG-Officers, HSG-Repairs, HSG-Income, HSG-Compliance, HSG-Finance and HSG-Admins.
    /// </summary>
    public static class HousingRoles
    {
        public const string Officer = "housing-officer";
        public const string Repairs = "repairs";
        public const string Income = "income";
        public const string Compliance = "compliance";
        public const string Finance = "finance";
        public const string Admin = "admin";
        public const string Tenant = "tenant";

        public static readonly string[] Staff = { Officer, Repairs, Income, Compliance, Finance, Admin };
    }

    public static class HousingClaimTypes
    {
        public const string Role = "role";
        public const string TenancyReference = "tenancy_ref";
        public const string TenantId = "tenant_id";
        public const string ContractorCode = "contractor_code";
        public const string Patch = "patch";
    }

    public static class HousingScopes
    {
        public const string ContractorApi = "contractor-api";
        public const string HousingProfile = "housing-profile";
    }
}
