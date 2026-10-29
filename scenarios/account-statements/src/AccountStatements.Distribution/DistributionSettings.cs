using System;
using System.Configuration;
using System.Globalization;

namespace Woodgrove.Statements.Distribution
{
    public class DistributionSettings
    {
        public int PollSeconds { get; set; } = 60;
        public int LeaseMinutes { get; set; } = 90;
        public int MaxSubscriptionsPerPoll { get; set; } = 3;
        public string ArchiveRoot { get; set; }
        public string StatementShareRoot { get; set; }
        public string SmtpHost { get; set; }
        public int SmtpPort { get; set; } = 25;
        public bool SmtpEnableSsl { get; set; }
        public string MailFrom { get; set; }
        public string ReplyTo { get; set; }
        public string RedirectCustomerMailTo { get; set; }
        public string PrintRoomNotify { get; set; }
        public bool PrintDuplex { get; set; } = true;
        public string LogFolder { get; set; }
        public string TempFolder { get; set; }
        public string HostName { get; set; } = Environment.MachineName;

        public static DistributionSettings FromConfig()
        {
            var s = ConfigurationManager.AppSettings;
            return new DistributionSettings
            {
                PollSeconds = Int(s["PollSeconds"], 60),
                LeaseMinutes = Int(s["LeaseMinutes"], 90),
                MaxSubscriptionsPerPoll = Int(s["MaxSubscriptionsPerPoll"], 3),
                ArchiveRoot = Required(s, "ArchiveRoot"),
                StatementShareRoot = Required(s, "StatementShareRoot"),
                SmtpHost = Required(s, "SmtpHost"),
                SmtpPort = Int(s["SmtpPort"], 25),
                SmtpEnableSsl = string.Equals(s["SmtpEnableSsl"], "true", StringComparison.OrdinalIgnoreCase),
                MailFrom = Required(s, "MailFrom"),
                ReplyTo = s["ReplyTo"],
                RedirectCustomerMailTo = s["RedirectCustomerMailTo"],
                PrintRoomNotify = s["PrintRoomNotify"],
                PrintDuplex = !string.Equals(s["PrintDuplex"], "false", StringComparison.OrdinalIgnoreCase),
                LogFolder = s["LogFolder"],
                TempFolder = s["TempFolder"]
            };
        }

        static string Required(System.Collections.Specialized.NameValueCollection s, string key)
        {
            var v = s[key];
            if (string.IsNullOrWhiteSpace(v)) throw new ConfigurationErrorsException("appSettings '" + key + "' is required.");
            return v;
        }

        static int Int(string value, int fallback)
        {
            int i;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out i) ? i : fallback;
        }
    }
}
