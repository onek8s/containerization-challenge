namespace VanArsdel.Integration.Runtime.Configuration
{
    /// <summary>Field names of the SSO config application (see bindings\SettingsFileGenerator.xml, SSO_ rows).</summary>
    public static class SettingNames
    {
        public const string IntegrationDb = "IntegrationDbConnectionString";
        public const string ArchiveRoot = "ArchiveRoot";
        public const string ErpServiceUrl = "ErpOrderServiceUrl";
        public const string ErpClientId = "ErpClientId";
        public const string ErpClientSecret = "ErpClientSecret";
        public const string SmtpHost = "SmtpHost";
        public const string AlertFrom = "AlertFromAddress";
        public const string OrderCutOffHour = "OrderCutOffHour";
        public const string DuplicateWindowHours = "DuplicateWindowHours";
        public const string BamEnabled = "BamTrackingEnabled";
    }
}
