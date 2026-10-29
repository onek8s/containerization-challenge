namespace Fabrikam.Housing.Integration
{
    public enum IntegrationMode
    {
        Live = 0,
        Simulated = 1
    }

    /// <summary>Bound from the "Integration" configuration section.</summary>
    public class IntegrationOptions
    {
        public IntegrationMode Mode { get; set; }
        /// <summary>Outbound web proxy for internet services, e.g. http://proxy.corp.local:8080. Empty for direct.</summary>
        public string ProxyUrl { get; set; }
        public bool ProxyUseDefaultCredentials { get; set; } = true;
        /// <summary>Folder where simulators keep their state and write what they would have sent.</summary>
        public string SimulatorFolder { get; set; } = @"D:\HousingData\simulators";

        public PaymentsOptions Payments { get; set; } = new PaymentsOptions();
        public SmsOptions Sms { get; set; } = new SmsOptions();
        public SmtpOptions Smtp { get; set; } = new SmtpOptions();
        public PostcodeOptions Postcodes { get; set; } = new PostcodeOptions();
        public GisOptions Gis { get; set; } = new GisOptions();
        public BacsBureauOptions BacsBureau { get; set; } = new BacsBureauOptions();
        public DocumentServiceOptions DocumentService { get; set; } = new DocumentServiceOptions();
    }

    public class PaymentsOptions
    {
        public string BaseUrl { get; set; } = "https://api.paystream.co.uk/v2/";
        public string AccessToken { get; set; }
        public string WebhookSecret { get; set; }
        public string CreditorId { get; set; }
    }

    public class SmsOptions
    {
        public string BaseUrl { get; set; } = "https://api.textline-gateway.co.uk/";
        public string ApiKey { get; set; }
        public string SenderId { get; set; } = "FabrikamHms";
    }

    public class SmtpOptions
    {
        public string Host { get; set; } = "smtp.corp.local";
        public int Port { get; set; } = 25;
        public bool UseStartTls { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string From { get; set; } = "Fabrikam Homes <noreply@fabrikamhomes.co.uk>";
    }

    public class PostcodeOptions
    {
        public string BaseUrl { get; set; } = "https://api.postcodes.io/";
        public int CacheHours { get; set; } = 24;
    }

    public class GisOptions
    {
        /// <summary>ArcGIS Server feature layer with the housing patch polygons.</summary>
        public string PatchLayerUrl { get; set; } = "https://gis.corp.local/arcgis/rest/services/Housing/Patches/MapServer/0/";
    }

    public class BacsBureauOptions
    {
        public string Host { get; set; } = "sftp.paybureau.co.uk";
        public int Port { get; set; } = 22;
        public string UserName { get; set; }
        public string PrivateKeyFile { get; set; } = @"D:\HousingData\keys\bacs\fabrikam_bureau.ppk";
        public string PrivateKeyPassphrase { get; set; }
        public string HostKeyFingerprint { get; set; }
        public string RemoteFolder { get; set; } = "/inbound";
        public string ServiceUserNumber { get; set; } = "412345";
        public string OriginSortCode { get; set; } = "30-96-26";
        public string OriginAccount { get; set; } = "01234567";
    }

    public class DocumentServiceOptions
    {
        public string BaseUrl { get; set; } = "http://docs01.corp.local:8085/";
        public string ApiKey { get; set; }
        public int TimeoutSeconds { get; set; } = 100;
    }
}
