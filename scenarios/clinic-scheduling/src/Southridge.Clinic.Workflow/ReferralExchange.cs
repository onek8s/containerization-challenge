using System;
using System.Workflow.Activities;

namespace Southridge.Clinic.Workflow
{
    /// <summary>Events the staff site and services raise into a running referral workflow.</summary>
    [ExternalDataExchange]
    public interface IReferralEvents
    {
        event EventHandler<ReferralDecisionEventArgs> Approved;

        event EventHandler<ReferralDecisionEventArgs> Declined;

        event EventHandler<ReferralDecisionEventArgs> Cancelled;

        event EventHandler<ReferralScheduledEventArgs> Scheduled;
    }

    /// <summary>Calls the workflow makes back into the host (implemented by the services host over the referral module).</summary>
    [ExternalDataExchange]
    public interface IReferralActions
    {
        void Received(int referralId, Guid instanceId);

        void Escalate(int referralId, int escalation, string reason);

        void SendDecisionLetter(int referralId, bool approved);

        void Expire(int referralId, string reason);

        void Completed(int referralId, string outcome);
    }

    [Serializable]
    public class ReferralDecisionEventArgs : ExternalDataEventArgs
    {
        public ReferralDecisionEventArgs(Guid instanceId, string user, string note)
            : base(instanceId)
        {
            User = user;
            Note = note;
            WaitForIdle = true;
        }

        public string User { get; private set; }

        public string Note { get; private set; }
    }

    [Serializable]
    public class ReferralScheduledEventArgs : ExternalDataEventArgs
    {
        public ReferralScheduledEventArgs(Guid instanceId, int appointmentId)
            : base(instanceId)
        {
            AppointmentId = appointmentId;
            WaitForIdle = true;
        }

        public int AppointmentId { get; private set; }
    }

    /// <summary>The ExternalDataExchange service registered with the runtime. Hosts call the Raise methods.</summary>
    public class ReferralEventService : IReferralEvents
    {
        public event EventHandler<ReferralDecisionEventArgs> Approved;

        public event EventHandler<ReferralDecisionEventArgs> Declined;

        public event EventHandler<ReferralDecisionEventArgs> Cancelled;

        public event EventHandler<ReferralScheduledEventArgs> Scheduled;

        public void RaiseApproved(Guid instanceId, string user, string note)
        {
            Raise(Approved, new ReferralDecisionEventArgs(instanceId, user, note));
        }

        public void RaiseDeclined(Guid instanceId, string user, string note)
        {
            Raise(Declined, new ReferralDecisionEventArgs(instanceId, user, note));
        }

        public void RaiseCancelled(Guid instanceId, string user, string note)
        {
            Raise(Cancelled, new ReferralDecisionEventArgs(instanceId, user, note));
        }

        public void RaiseScheduled(Guid instanceId, int appointmentId)
        {
            EventHandler<ReferralScheduledEventArgs> handler = Scheduled;
            if (handler != null)
            {
                handler(null, new ReferralScheduledEventArgs(instanceId, appointmentId));
            }
        }

        private static void Raise(EventHandler<ReferralDecisionEventArgs> handler, ReferralDecisionEventArgs args)
        {
            if (handler != null)
            {
                handler(null, args);
            }
        }
    }
}
