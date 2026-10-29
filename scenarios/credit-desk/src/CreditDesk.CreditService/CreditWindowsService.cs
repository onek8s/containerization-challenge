using System.ServiceProcess;

namespace CreditDesk.CreditService
{
    internal sealed class CreditWindowsService : ServiceBase
    {
        public const string Name = "CreditDeskCredit";
        private CreditServiceHost _host;

        public CreditWindowsService()
        {
            ServiceName = Name;
            CanStop = true;
            CanShutdown = true;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            _host = new CreditServiceHost();
            _host.Start();
        }

        protected override void OnStop()
        {
            _host?.Stop();
            _host = null;
        }

        protected override void OnShutdown() => OnStop();
    }
}
