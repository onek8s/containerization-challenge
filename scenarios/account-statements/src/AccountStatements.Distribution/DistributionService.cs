using System;
using System.ServiceProcess;
using Woodgrove.Statements.Data;

namespace Woodgrove.Statements.Distribution
{
    public class DistributionService : ServiceBase
    {
        public const string Name = "WoodgroveStatementDistribution";
        DistributionWorker _worker;

        public DistributionService()
        {
            ServiceName = Name;
            CanStop = true;
            CanShutdown = true;
            CanPauseAndContinue = true;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            var settings = DistributionSettings.FromConfig();
            Log.Initialize(settings.LogFolder);
            _worker = new DistributionWorker(settings, Db.FromConfig());
            _worker.Start();
        }

        protected override void OnStop()
        {
            RequestAdditionalTime(120000);
            _worker?.Stop(TimeSpan.FromSeconds(110));
        }

        protected override void OnShutdown()
        {
            OnStop();
        }

        protected override void OnPause()
        {
            _worker?.Stop(TimeSpan.FromSeconds(110));
            Log.Info("Distribution paused");
        }

        protected override void OnContinue()
        {
            _worker?.Start();
            Log.Info("Distribution continued");
        }
    }
}
