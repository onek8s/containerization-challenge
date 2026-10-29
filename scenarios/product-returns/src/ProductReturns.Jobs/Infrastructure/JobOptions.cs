namespace ProductReturns.Jobs.Infrastructure
{
    public class JobOptions
    {
        /// <summary>Windows time zone of the head office; the refund cut-off is local midnight.</summary>
        public string TimeZone { get; set; } = "W. Europe Standard Time";

        /// <summary>Finance picks up refund batches from this folder.</summary>
        public string RefundOutbox { get; set; } = @"\\files01.corp.local\Finance$\Refunds\Inbound";

        /// <summary>Copy of every refund batch that was delivered.</summary>
        public string RefundArchive { get; set; } = @"D:\ReturnsData\Jobs\Archive";

        public string StateFolder { get; set; } = @"D:\ReturnsData\Jobs";

        public string LogFolder { get; set; } = @"D:\ReturnsData\Jobs\Logs";

        /// <summary>Label share, the same folder the label service writes to.</summary>
        public string LabelShare { get; set; } = @"\\files01.corp.local\Returns$\Labels";

        public int LabelRetentionDays { get; set; } = 180;

        public int AnonymizeAfterDays { get; set; } = 730;
    }

    public class JobMailOptions
    {
        public string SmtpHost { get; set; } = "smtp.corp.local";
        public int SmtpPort { get; set; } = 25;
        public bool UseDefaultCredentials { get; set; } = true;
        public string PickupDirectory { get; set; }
        public string From { get; set; } = "returns@wideworldimporters.example";
        public string FromName { get; set; } = "Wide World Importers Returns";
        public string FinanceTo { get; set; } = "ap-refunds@wideworldimporters.example";
    }
}
