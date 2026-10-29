namespace ProductReturns.Labels.Services
{
    public class LabelOptions
    {
        /// <summary>Label PDFs are written here, one folder per year and month.</summary>
        public string SharePath { get; set; } = @"\\files01.corp.local\Returns$\Labels";

        /// <summary>Local working folder for the daily quota counter.</summary>
        public string StateFolder { get; set; } = @"D:\ReturnsData\Labels";

        public int PollSeconds { get; set; } = 60;

        public int BatchSize { get; set; } = 20;

        public int MaxAttempts { get; set; } = 5;

        /// <summary>Labels per day included in the carrier contract.</summary>
        public int DailyQuota { get; set; } = 400;

        public string WarehouseName { get; set; } = "Wide World Importers Returns";
        public string WarehouseStreet { get; set; } = "Waalhaven Z.z. 18";
        public string WarehousePostalCode { get; set; } = "3089 JH";
        public string WarehouseCity { get; set; } = "Rotterdam";
        public string WarehouseCountry { get; set; } = "NL";
    }

    public class CarrierOptions
    {
        /// <summary>"ParcelLink" (default) or "File" for development.</summary>
        public string Provider { get; set; } = "ParcelLink";

        public string BaseUrl { get; set; } = "https://api.parcellink.example/";

        public string AccountNumber { get; set; }

        /// <summary>API key, protected with "ProductReturns.Labels.exe protect-secret" on this server.</summary>
        public string ApiKeyProtected { get; set; }

        public string Proxy { get; set; }

        public int TimeoutSeconds { get; set; } = 30;
    }

    public class MailOptions
    {
        public string SmtpHost { get; set; } = "smtp.corp.local";
        public int SmtpPort { get; set; } = 25;
        public bool UseDefaultCredentials { get; set; } = true;

        /// <summary>When set, mails are written to this folder instead of being sent.</summary>
        public string PickupDirectory { get; set; }

        public string From { get; set; } = "returns@wideworldimporters.example";
        public string FromName { get; set; } = "Wide World Importers Returns";
        public string Bcc { get; set; }
        public string TemplateFolder { get; set; } = "Templates";
        public string LabelTemplate { get; set; } = "ReturnLabel.html";
    }
}
