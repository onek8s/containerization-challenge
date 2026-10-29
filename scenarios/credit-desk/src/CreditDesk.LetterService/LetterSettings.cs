using System.Configuration;

namespace CreditDesk.LetterService
{
    internal sealed class LetterSettings
    {
        public string QueuePath { get; private set; }
        public string FailedQueuePath { get; private set; }
        public string TemplatePath { get; private set; }
        public string OutputFolder { get; private set; }
        public string Printer { get; private set; }
        public int PrintCopies { get; private set; }
        public int MaxAttempts { get; private set; }
        public int RecycleWordAfter { get; private set; }
        public string ConnectionString { get; private set; }

        public static LetterSettings Load()
        {
            var s = ConfigurationManager.AppSettings;
            return new LetterSettings
            {
                QueuePath = s["QueuePath"] ?? @".\private$\creditdesk.letters",
                FailedQueuePath = s["FailedQueuePath"] ?? @".\private$\creditdesk.letters.failed",
                TemplatePath = s["TemplatePath"],
                OutputFolder = s["OutputFolder"],
                Printer = s["Printer"],
                PrintCopies = int.TryParse(s["PrintCopies"], out var copies) ? copies : 1,
                MaxAttempts = int.TryParse(s["MaxAttempts"], out var attempts) ? attempts : 3,
                RecycleWordAfter = int.TryParse(s["RecycleWordAfter"], out var recycle) ? recycle : 50,
                ConnectionString = ConfigurationManager.ConnectionStrings["CreditDesk"].ConnectionString
            };
        }
    }
}
