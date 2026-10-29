using System;
using System.Workflow.Activities;
using System.Workflow.ComponentModel;

namespace Southridge.Clinic.Workflow
{
    /// <summary>
    /// Referral approval: waits for a coordinator decision with escalation on timeout, then waits for the
    /// approved referral to be scheduled. Instances are persisted in the SouthridgeWorkflow database while idle.
    /// </summary>
    public sealed partial class ReferralApprovalWorkflow : SequentialWorkflowActivity
    {
        public const int MaxEscalations = 3;
        public const int MaxSchedulingReminders = 2;

        public static readonly DependencyProperty ReferralIdProperty =
            DependencyProperty.Register("ReferralId", typeof(int), typeof(ReferralApprovalWorkflow));

        public static readonly DependencyProperty PriorityProperty =
            DependencyProperty.Register("Priority", typeof(int), typeof(ReferralApprovalWorkflow));

        // Fields bound to the CallExternalMethod parameters.
        public Guid CurrentInstanceId = Guid.Empty;
        public int EscalationCount;
        public string EscalationReason = string.Empty;
        public bool DecisionApproved;
        public string ExpireReason = string.Empty;
        public string Outcome = string.Empty;

        private string _decision = string.Empty;
        private int _schedulingReminders;
        private bool _scheduled;

        public ReferralApprovalWorkflow()
        {
            InitializeComponent();
        }

        /// <summary>Referral key in dbo.Referral.</summary>
        public int ReferralId
        {
            get { return (int)GetValue(ReferralIdProperty); }
            set { SetValue(ReferralIdProperty, value); }
        }

        /// <summary>0 routine, 1 urgent, 2 stat (ReferralPriority).</summary>
        public int Priority
        {
            get { return (int)GetValue(PriorityProperty); }
            set { SetValue(PriorityProperty, value); }
        }

        public static TimeSpan ReviewTimeout(int priority)
        {
            switch (priority)
            {
                case 2: return TimeSpan.FromHours(4);
                case 1: return TimeSpan.FromHours(24);
                default: return TimeSpan.FromHours(72);
            }
        }

        private void OnStart(object sender, EventArgs e)
        {
            CurrentInstanceId = WorkflowInstanceId;
        }

        private void AwaitingDecision(object sender, ConditionalEventArgs e)
        {
            e.Result = _decision.Length == 0;
        }

        private void OnApproved(object sender, ExternalDataEventArgs e)
        {
            _decision = "Approved";
            DecisionApproved = true;
        }

        private void OnDeclined(object sender, ExternalDataEventArgs e)
        {
            _decision = "Declined";
            DecisionApproved = false;
        }

        private void OnCancelled(object sender, ExternalDataEventArgs e)
        {
            _decision = "Cancelled";
        }

        private void SetReviewTimeout(object sender, EventArgs e)
        {
            ((DelayActivity)sender).TimeoutDuration = ReviewTimeout(Priority);
        }

        private void OnReviewTimeout(object sender, EventArgs e)
        {
            EscalationCount++;
            EscalationReason = "No decision after " + ReviewTimeout(Priority).TotalHours + " hours (escalation " + EscalationCount + ").";
        }

        private void AfterEscalation(object sender, EventArgs e)
        {
            if (EscalationCount >= MaxEscalations)
            {
                _decision = "Expired";
                ExpireReason = "Expired: no decision after " + MaxEscalations + " escalations.";
            }
        }

        private void IsApproved(object sender, ConditionalEventArgs e)
        {
            e.Result = _decision == "Approved";
        }

        private void IsDeclined(object sender, ConditionalEventArgs e)
        {
            e.Result = _decision == "Declined";
        }

        private void IsExpired(object sender, ConditionalEventArgs e)
        {
            e.Result = _decision == "Expired";
        }

        private void AwaitingAppointment(object sender, ConditionalEventArgs e)
        {
            e.Result = !_scheduled && _decision == "Approved" && _schedulingReminders <= MaxSchedulingReminders;
        }

        private void OnScheduled(object sender, ExternalDataEventArgs e)
        {
            _scheduled = true;
        }

        private void OnScheduleCancelled(object sender, ExternalDataEventArgs e)
        {
            _decision = "Cancelled";
        }

        private void OnSchedulingTimeout(object sender, EventArgs e)
        {
            _schedulingReminders++;
            EscalationCount++;
            EscalationReason = "Approved referral not scheduled after " + (_schedulingReminders * 14) + " days.";
        }

        private void NotScheduled(object sender, ConditionalEventArgs e)
        {
            e.Result = _decision == "Approved" && !_scheduled;
            if (e.Result)
            {
                ExpireReason = "Expired: approved but never scheduled.";
            }
        }

        private void OnComplete(object sender, EventArgs e)
        {
            Outcome = _scheduled ? "Scheduled" : _decision;
        }
    }
}
