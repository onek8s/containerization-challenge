using System;
using System.ServiceModel;
using System.ServiceModel.Activation;
using Southridge.Clinic.Contracts;
using Southridge.Clinic.Infrastructure;
using Southridge.Clinic.Referrals;
using Southridge.Clinic.Workflow;

namespace Southridge.Clinic.Services
{
    /// <summary>
    /// Forwards referral decisions from the staff site to the WF runtime hosted in this application. The
    /// decision itself is already saved by the caller; this only moves the workflow on.
    /// </summary>
    [ServiceBehavior(Namespace = Namespaces.Records, InstanceContextMode = InstanceContextMode.PerCall)]
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
    public class ReferralWorkflowService : IReferralWorkflowService
    {
        public void Start(int referralId)
        {
            ServiceCall.Run("Start", delegate
            {
                int priority = ReferralSessions.InUnitOfWork(s => (int)s.Load<Referral>(referralId).Priority);
                StartWorkflow(referralId, priority);
            });
        }

        public void Decided(int referralId, bool approved, string user, string note)
        {
            ServiceCall.Run("Decided", delegate
            {
                Guid? instance = InstanceOf(referralId);
                if (instance.HasValue)
                {
                    if (approved)
                    {
                        Host.Approve(instance.Value, user, note);
                    }
                    else
                    {
                        Host.Decline(instance.Value, user, note);
                    }
                }
            });
        }

        public void Scheduled(int referralId, int appointmentId)
        {
            ServiceCall.Run("Scheduled", delegate
            {
                Guid? instance = InstanceOf(referralId);
                if (instance.HasValue)
                {
                    Host.Scheduled(instance.Value, appointmentId);
                }
            });
        }

        public void Cancelled(int referralId, string user, string note)
        {
            ServiceCall.Run("Cancelled", delegate
            {
                Guid? instance = InstanceOf(referralId);
                if (instance.HasValue)
                {
                    Host.Cancel(instance.Value, user, note);
                }
            });
        }

        internal static ReferralWorkflowHost Host
        {
            get
            {
                if (Global.Workflows == null)
                {
                    throw new InvalidOperationException("The referral workflow runtime is not running.");
                }

                return Global.Workflows;
            }
        }

        internal static void StartWorkflow(int referralId, int priority)
        {
            if (Global.Workflows == null)
            {
                ClinicLog.Warning(LogCategory.General, "Workflow runtime disabled; referral " + referralId + " will be started when it is enabled.");
                return;
            }

            Guid id = Global.Workflows.StartReferral(referralId, priority);
            ClinicLog.Info(LogCategory.General, "Referral {0} workflow {1} started.", referralId, id);
        }

        /// <summary>Called when an approved referral gets its appointment through the scheduling service.</summary>
        internal static void LinkAppointment(int referralId, int appointmentId, string user)
        {
            Guid? instance = ReferralSessions.InUnitOfWork(session =>
            {
                Referral referral = session.Get<Referral>(referralId);
                if (referral == null || referral.Status != Southridge.Clinic.Domain.ReferralStatus.Approved)
                {
                    return (Guid?)null;
                }

                new ReferralService(session, ClinicContainer.Resolve<IClock>()).LinkAppointment(referral, appointmentId, user);
                return referral.WorkflowInstanceId;
            });
            if (instance.HasValue && Global.Workflows != null)
            {
                Global.Workflows.Scheduled(instance.Value, appointmentId);
            }
        }

        private static Guid? InstanceOf(int referralId)
        {
            return ReferralSessions.InUnitOfWork(s =>
            {
                Referral r = s.Get<Referral>(referralId);
                if (r == null)
                {
                    throw new NotFoundException("Referral " + referralId + " was not found.");
                }

                return r.WorkflowInstanceId;
            });
        }
    }
}
