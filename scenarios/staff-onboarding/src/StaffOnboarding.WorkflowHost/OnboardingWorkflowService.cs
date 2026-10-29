using System;
using System.Diagnostics;
using System.ServiceProcess;
using StaffOnboarding.Workflows.Activities;

namespace StaffOnboarding.WorkflowHost
{
    public class OnboardingWorkflowService : ServiceBase
    {
        public const string Name = "StaffOnboardingWorkflow";
        private HostSet _hosts;

        public OnboardingWorkflowService()
        {
            ServiceName = Name;
            CanStop = true;
            CanShutdown = true;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            // Opening the hosts recovers persisted instances with expired timers, which can take a while
            // after a long outage.
            RequestAdditionalTime((int)TimeSpan.FromMinutes(2).TotalMilliseconds);
            try
            {
                _hosts = new HostSet();
                _hosts.Open();
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(HostLog.Source, "Workflow host failed to start: " + ex, EventLogEntryType.Error, 3001);
                throw;
            }
        }

        protected override void OnStop()
        {
            RequestAdditionalTime((int)TimeSpan.FromMinutes(1).TotalMilliseconds);
            if (_hosts != null)
            {
                _hosts.Dispose();
                _hosts = null;
            }
        }

        protected override void OnShutdown()
        {
            OnStop();
        }
    }
}
