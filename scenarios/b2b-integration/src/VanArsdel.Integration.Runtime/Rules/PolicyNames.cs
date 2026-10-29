namespace VanArsdel.Integration.Runtime.Rules
{
    /// <summary>BRE policies deployed with the application (rules\*.xml). The latest deployed version is executed.</summary>
    public static class PolicyNames
    {
        public const string OrderValidation = "VanArsdel.OrderValidation";
        public const string CreditRelease = "VanArsdel.CreditRelease";
        public const string Routing = "VanArsdel.Routing";
    }
}
