using System.ServiceProcess;
using log4net;
using Microsoft.Practices.Unity;

namespace VanArsdel.Integration.SlaMonitor
{
    public class SlaMonitorService : ServiceBase
    {
        public const string Name = "VanArsdelB2BSlaMonitor";
        private static readonly ILog Log = LogManager.GetLogger(typeof(SlaMonitorService));
        private IUnityContainer _container;
        private MonitorLoop _loop;

        public SlaMonitorService()
        {
            ServiceName = Name;
            CanStop = true;
            CanShutdown = true;
            CanPauseAndContinue = true;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            _container = ContainerConfig.Build();
            _loop = _container.Resolve<MonitorLoop>();
            _loop.Start();
            Log.Info("Service started");
        }

        protected override void OnStop()
        {
            if (_loop != null)
            {
                _loop.Stop();
            }

            if (_container != null)
            {
                _container.Dispose();
            }

            Log.Info("Service stopped");
        }

        protected override void OnPause()
        {
            _loop.Pause();
        }

        protected override void OnContinue()
        {
            _loop.Resume();
        }

        protected override void OnShutdown()
        {
            OnStop();
        }
    }
}
