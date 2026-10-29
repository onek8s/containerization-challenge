using System;
using System.ServiceProcess;

namespace Coho.Billing.Engine
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var command = args.Length > 0 ? args[0].ToLowerInvariant() : string.Empty;

            switch (command)
            {
                case "install-eventlog":
                    EngineLog.InstallSource();
                    Console.WriteLine("Event source '{0}' installed.", EngineLog.EventSource);
                    return 0;

                case "console":
                    return RunConsole();

                case "":
                    ServiceBase.Run(new BillingEngineService());
                    return 0;

                default:
                    Console.Error.WriteLine("Usage: MeterBilling.Engine.exe [console | install-eventlog]");
                    return 1;
            }
        }

        private static int RunConsole()
        {
            var settings = EngineSettings.Load();
            EngineLog.Initialize(settings.LogFolder, true);

            using (var engine = new BillingEngine(settings))
            {
                engine.Start();
                Console.WriteLine("Meter Billing engine running, control endpoint {0}. Press Enter to stop.", settings.ControlUrl);
                Console.ReadLine();
                engine.Stop();
            }

            return 0;
        }
    }

    public class BillingEngineService : ServiceBase
    {
        public const string Name = "CohoMeterBilling";
        private BillingEngine _engine;

        public BillingEngineService()
        {
            ServiceName = Name;
            CanPauseAndContinue = true;
            CanShutdown = true;
        }

        protected override void OnStart(string[] args)
        {
            var settings = EngineSettings.Load();
            EngineLog.Initialize(settings.LogFolder, false);
            _engine = new BillingEngine(settings);
            _engine.Start();
        }

        protected override void OnPause()
        {
            _engine.Pause("service control manager");
        }

        protected override void OnContinue()
        {
            _engine.Continue("service control manager");
        }

        protected override void OnStop()
        {
            if (_engine != null)
            {
                _engine.Stop();
                _engine.Dispose();
                _engine = null;
            }
        }

        protected override void OnShutdown()
        {
            OnStop();
        }
    }
}
