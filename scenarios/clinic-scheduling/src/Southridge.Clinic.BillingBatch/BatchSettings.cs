using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using Southridge.Clinic.Billing;

namespace Southridge.Clinic.BillingBatch
{
    /// <summary>appSettings for the nightly billing run (Billing:*). Folders are relative to the program folder.</summary>
    public class BatchSettings
    {
        public string WorkFolder { get; set; }
        public string StatementFolder { get; set; }
        public string ReportFolder { get; set; }
        public string Transport { get; set; }
        public string ExchangeOutbound { get; set; }
        public string ExchangeInbound { get; set; }
        public string SftpHost { get; set; }
        public int SftpPort { get; set; }
        public string SftpUser { get; set; }
        public string SftpPassword { get; set; }
        public string SftpKeyFile { get; set; }
        public int TimelyFilingDays { get; set; }
        public SubmitterInfo Submitter { get; set; }

        public static BatchSettings Load()
        {
            return new BatchSettings
            {
                WorkFolder = Folder("Billing:WorkFolder", @"..\work\billing"),
                StatementFolder = Folder("Billing:StatementFolder", @"..\output\statements"),
                ReportFolder = Folder("Billing:ReportFolder", @"..\output\reports"),
                Transport = Get("Billing:Transport", "Folder"),
                ExchangeOutbound = Get("Billing:ExchangeOutbound", @"..\exchange\to-clearinghouse"),
                ExchangeInbound = Get("Billing:ExchangeInbound", @"..\exchange\from-clearinghouse"),
                SftpHost = Get("Billing:SftpHost", null),
                SftpPort = int.Parse(Get("Billing:SftpPort", "22"), CultureInfo.InvariantCulture),
                SftpUser = Get("Billing:SftpUser", null),
                SftpPassword = Get("Billing:SftpPassword", null),
                SftpKeyFile = Get("Billing:SftpKeyFile", null),
                TimelyFilingDays = int.Parse(Get("Billing:TimelyFilingDays", "90"), CultureInfo.InvariantCulture),
                Submitter = new SubmitterInfo
                {
                    SenderId = Get("Billing:SenderId", "SMGCLINIC"),
                    ReceiverId = Get("Billing:ReceiverId", "CLEARHOUSE"),
                    SubmitterName = Get("Billing:SubmitterName", "SOUTHRIDGE MEDICAL GROUP"),
                    ContactName = Get("Billing:ContactName", "BILLING OFFICE"),
                    ContactPhone = Get("Billing:ContactPhone", "6145550100"),
                    ReceiverName = Get("Billing:ReceiverName", "CLEARINGHOUSE"),
                    BillingProviderName = Get("Billing:BillingProviderName", "SOUTHRIDGE MEDICAL GROUP"),
                    Production = string.Equals(Get("Billing:Production", "false"), "true", StringComparison.OrdinalIgnoreCase)
                }
            };
        }

        private static string Get(string key, string defaultValue)
        {
            string value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        private static string Folder(string key, string defaultValue)
        {
            string value = Get(key, defaultValue);
            return Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, value));
        }
    }
}
