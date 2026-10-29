using System;
using System.Configuration;
using System.Globalization;
using Coho.Billing.Core.Output;
using Coho.Billing.Core.Reads;

namespace Coho.Billing.Engine
{
    public class EngineSettings
    {
        public string ReadInbox { get; set; }
        public string ReadArchive { get; set; }
        public string ReadRejected { get; set; }
        public TimeSpan FileSettle { get; set; }
        public string PrintOutput { get; set; }
        public string BankOutput { get; set; }
        public TimeSpan WorkInterval { get; set; }
        public string LogFolder { get; set; }
        public string ControlUrl { get; set; }
        public string OperatorsGroup { get; set; }
        public string[] ReprintCallers { get; set; }
        public ReadCheckSettings ReadChecks { get; set; }
        public decimal AutoReleaseLimit { get; set; }
        public int BillDateOffsetDays { get; set; }
        public CreditorSettings Creditor { get; set; }

        public static EngineSettings Load()
        {
            return new EngineSettings
            {
                ReadInbox = Required("ReadInbox"),
                ReadArchive = Required("ReadArchive"),
                ReadRejected = Required("ReadRejected"),
                FileSettle = TimeSpan.FromSeconds(Int("FileSettleSeconds", 30)),
                PrintOutput = Required("PrintOutput"),
                BankOutput = Required("BankOutput"),
                WorkInterval = TimeSpan.FromSeconds(Int("WorkIntervalSeconds", 60)),
                LogFolder = Required("LogFolder"),
                ControlUrl = Required("ControlUrl"),
                OperatorsGroup = Required("OperatorsGroup"),
                ReprintCallers = (ConfigurationManager.AppSettings["ReprintCallers"] ?? string.Empty)
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries),
                ReadChecks = new ReadCheckSettings
                {
                    HighFactor = Decimal("HighFactor", 3.0m),
                    LowFactor = Decimal("LowFactor", 0.2m),
                    MinimumCheckedConsumption = Decimal("MinimumCheckedConsumption", 5m)
                },
                AutoReleaseLimit = Decimal("AutoReleaseLimit", 0m),
                BillDateOffsetDays = Int("BillDateOffsetDays", 3),
                Creditor = new CreditorSettings
                {
                    Name = Required("CreditorName"),
                    Iban = Required("CreditorIban"),
                    Bic = Required("CreditorBic"),
                    CreditorId = Required("CreditorId")
                }
            };
        }

        private static string Required(string key)
        {
            var value = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ConfigurationErrorsException("Setting '" + key + "' is missing");
            }

            return value.Trim();
        }

        private static int Int(string key, int fallback)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrEmpty(value) ? fallback : int.Parse(value, CultureInfo.InvariantCulture);
        }

        private static decimal Decimal(string key, decimal fallback)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrEmpty(value) ? fallback : decimal.Parse(value, CultureInfo.InvariantCulture);
        }
    }
}
