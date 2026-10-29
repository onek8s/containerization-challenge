using System;
using System.Collections.Generic;
using System.Web;
using Southridge.Clinic.Infrastructure;
using Southridge.Clinic.Referrals;
using Southridge.Clinic.Workflow;

namespace Southridge.Clinic.Services
{
    public class Global : HttpApplication
    {
        /// <summary>The WF runtime for referrals, or null when Clinic:WorkflowEnabled is false.</summary>
        internal static ReferralWorkflowHost Workflows { get; private set; }

        protected void Application_Start(object sender, EventArgs e)
        {
            ClinicContainer.ApplyConfiguration(ClinicContainer.Current);
            ReferralSessions.Initialize("thread_static");
            ClinicLog.Info(LogCategory.General, "Clinic services starting ({0}).", typeof(Global).Assembly.GetName().Version);

            if (ClinicSettings.GetBool("WorkflowEnabled", true))
            {
                var actions = new ReferralActions(ClinicContainer.Resolve<IMailer>(), ClinicContainer.Resolve<IClock>());
                var host = new ReferralWorkflowHost(ClinicSettings.ConnectionString("Workflow"), actions,
                    ClinicSettings.GetTimeSpan("WorkflowLoadingInterval", TimeSpan.FromMinutes(2)));
                host.Failed += delegate(object s, WorkflowFailedEventArgs args)
                {
                    ClinicLog.Error(LogCategory.General, "Referral workflow " + args.InstanceId + " terminated.", args.Exception);
                };
                host.Start();
                Workflows = host;
                StartPendingReferrals();
            }
        }

        protected void Application_End(object sender, EventArgs e)
        {
            if (Workflows != null)
            {
                Workflows.Dispose();
                Workflows = null;
            }

            ClinicLog.Info(LogCategory.General, "Clinic services stopped.");
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            Exception ex = Server.GetLastError();
            if (ex != null)
            {
                ClinicLog.Error(LogCategory.General, "Unhandled error in " + Request.Path, ex.GetBaseException());
            }
        }

        /// <summary>Referrals received while the services were down (or entered by the staff site) get their workflow here.</summary>
        private static void StartPendingReferrals()
        {
            try
            {
                IList<Referral> pending = ReferralSessions.InUnitOfWork(s => new ReferralRepository(s).WithoutWorkflow());
                foreach (Referral r in pending)
                {
                    Workflows.StartReferral(r.Id, (int)r.Priority);
                }

                if (pending.Count > 0)
                {
                    ClinicLog.Info(LogCategory.General, "Started workflows for {0} pending referrals.", pending.Count);
                }
            }
            catch (Exception ex)
            {
                ClinicLog.Error(LogCategory.General, "Starting pending referral workflows failed.", ex);
            }
        }
    }
}
