namespace Northwind.Inspections.Core
{
    /// <summary>Value names under HKLM\SOFTWARE\Northwind Insurance\Inspections.</summary>
    public static class SettingNames
    {
        public const string RegistryKey = @"SOFTWARE\Northwind Insurance\Inspections";
        public const string SecretsSubKey = "Secrets";

        public const string ConnectionString = "ConnectionString";
        public const string PhotoShare = "PhotoShare";
        public const string ShareUser = "ShareUser";
        public const string SharePassword = "SharePassword";
        public const string SmtpHost = "SmtpHost";
        public const string SmtpPort = "SmtpPort";
        public const string SmtpFrom = "SmtpFrom";
        public const string SmtpUser = "SmtpUser";
        public const string SmtpPassword = "SmtpPassword";
        public const string PortalUrl = "PortalUrl";
        public const string MaxPhotoEdge = "MaxPhotoEdge";
        public const string ThumbnailEdge = "ThumbnailEdge";
        public const string JpegQuality = "JpegQuality";
        public const string WatermarkFont = "WatermarkFont";
        public const string PurgeAfterDays = "PurgeAfterDays";
        public const string ReportBatchSize = "ReportBatchSize";
        public const string WorkFolder = "WorkFolder";
    }
}
