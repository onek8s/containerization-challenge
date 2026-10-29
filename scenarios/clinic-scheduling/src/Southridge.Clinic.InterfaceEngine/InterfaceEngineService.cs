using System;
using System.ServiceProcess;
using log4net;

namespace Southridge.Clinic.InterfaceEngine
{
    public class InterfaceEngineService : ServiceBase
    {
        public const string Name = "SouthridgeInterfaceEngine";

        private static readonly ILog Log = LogManager.GetLogger(typeof(InterfaceEngineService));
        private EngineHost _host;

        public InterfaceEngineService()
        {
            ServiceName = Name;
            CanStop = true;
            CanShutdown = true;
            CanPauseAndContinue = false;
            AutoLog = false;
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                _host = new EngineHost();
                _host.Start();
            }
            catch (Exception ex)
            {
                Log.Fatal("Interface engine failed to start.", ex);
                ExitCode = 1064;
                throw;
            }
        }

        protected override void OnStop()
        {
            if (_host != null)
            {
                _host.Stop();
                _host = null;
            }
        }

        protected override void OnShutdown()
        {
            OnStop();
        }
    }
}
