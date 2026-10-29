using System;
using System.ServiceProcess;

namespace CreditDesk.LetterService
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "/console", StringComparison.OrdinalIgnoreCase))
            {
                var processor = new LetterProcessor(LetterSettings.Load());
                processor.Start();
                Console.WriteLine("Processing letters from " + processor.QueuePath + ". Press Enter to stop.");
                Console.ReadLine();
                processor.Stop();
                return 0;
            }

            ServiceBase.Run(new LetterWindowsService());
            return 0;
        }
    }

    internal sealed class LetterWindowsService : ServiceBase
    {
        public const string Name = "CreditDeskLetters";
        private LetterProcessor _processor;

        public LetterWindowsService()
        {
            ServiceName = Name;
            CanStop = true;
            CanShutdown = true;
        }

        protected override void OnStart(string[] args)
        {
            RequestAdditionalTime(30000);
            _processor = new LetterProcessor(LetterSettings.Load());
            _processor.Start();
        }

        protected override void OnStop()
        {
            RequestAdditionalTime(60000);
            _processor?.Stop();
            _processor = null;
        }

        protected override void OnShutdown() => OnStop();
    }
}
