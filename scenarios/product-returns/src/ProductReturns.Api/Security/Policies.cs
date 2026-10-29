namespace ProductReturns.Api.Security
{
    public static class Policies
    {
        public const string Portal = "Portal";
        public const string Scanner = "Scanner";
    }

    public static class Roles
    {
        public const string Portal = "Portal";
        public const string Scanner = "Scanner";
    }

    public static class Headers
    {
        public const string ApiKey = "X-Api-Key";

        /// <summary>Staff member or customer the portal is acting for. Only trusted from portal callers.</summary>
        public const string OnBehalfOf = "X-Returns-User";
    }
}
