using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Workflow.Activities;
using System.Workflow.ComponentModel;
using System.Workflow.ComponentModel.Design;

namespace Southridge.Clinic.Workflow
{
    partial class ReferralApprovalWorkflow
    {
        #region Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCode]
        private void InitializeComponent()
        {
            this.CanModifyActivities = true;
            this.codeStart = new System.Workflow.Activities.CodeActivity();
            this.callReceived = new System.Workflow.Activities.CallExternalMethodActivity();
            this.whileAwaitingDecision = new System.Workflow.Activities.WhileActivity();
            this.listenDecision = new System.Workflow.Activities.ListenActivity();
            this.eventApproved = new System.Workflow.Activities.EventDrivenActivity();
            this.handleApproved = new System.Workflow.Activities.HandleExternalEventActivity();
            this.eventDeclined = new System.Workflow.Activities.EventDrivenActivity();
            this.handleDeclined = new System.Workflow.Activities.HandleExternalEventActivity();
            this.eventCancelled = new System.Workflow.Activities.EventDrivenActivity();
            this.handleCancelled = new System.Workflow.Activities.HandleExternalEventActivity();
            this.eventReviewTimeout = new System.Workflow.Activities.EventDrivenActivity();
            this.delayReview = new System.Workflow.Activities.DelayActivity();
            this.codeReviewTimeout = new System.Workflow.Activities.CodeActivity();
            this.callEscalate = new System.Workflow.Activities.CallExternalMethodActivity();
            this.codeAfterEscalation = new System.Workflow.Activities.CodeActivity();
            this.ifDecision = new System.Workflow.Activities.IfElseActivity();
            this.branchApproved = new System.Workflow.Activities.IfElseBranchActivity();
            this.callApprovedLetter = new System.Workflow.Activities.CallExternalMethodActivity();
            this.whileAwaitingAppointment = new System.Workflow.Activities.WhileActivity();
            this.listenAppointment = new System.Workflow.Activities.ListenActivity();
            this.eventScheduled = new System.Workflow.Activities.EventDrivenActivity();
            this.handleScheduled = new System.Workflow.Activities.HandleExternalEventActivity();
            this.eventScheduleCancelled = new System.Workflow.Activities.EventDrivenActivity();
            this.handleScheduleCancelled = new System.Workflow.Activities.HandleExternalEventActivity();
            this.eventSchedulingTimeout = new System.Workflow.Activities.EventDrivenActivity();
            this.delayScheduling = new System.Workflow.Activities.DelayActivity();
            this.codeSchedulingTimeout = new System.Workflow.Activities.CodeActivity();
            this.callSchedulingEscalate = new System.Workflow.Activities.CallExternalMethodActivity();
            this.branchDeclined = new System.Workflow.Activities.IfElseBranchActivity();
            this.callDeclinedLetter = new System.Workflow.Activities.CallExternalMethodActivity();
            this.branchExpired = new System.Workflow.Activities.IfElseBranchActivity();
            this.callExpire = new System.Workflow.Activities.CallExternalMethodActivity();
            this.ifNotScheduled = new System.Workflow.Activities.IfElseActivity();
            this.branchNotScheduled = new System.Workflow.Activities.IfElseBranchActivity();
            this.callExpireUnscheduled = new System.Workflow.Activities.CallExternalMethodActivity();
            this.codeComplete = new System.Workflow.Activities.CodeActivity();
            this.callCompleted = new System.Workflow.Activities.CallExternalMethodActivity();
            System.Workflow.Activities.CodeCondition codecondition1 = new System.Workflow.Activities.CodeCondition();
            System.Workflow.Activities.CodeCondition codecondition2 = new System.Workflow.Activities.CodeCondition();
            System.Workflow.Activities.CodeCondition codecondition3 = new System.Workflow.Activities.CodeCondition();
            System.Workflow.Activities.CodeCondition codecondition4 = new System.Workflow.Activities.CodeCondition();
            System.Workflow.Activities.CodeCondition codecondition5 = new System.Workflow.Activities.CodeCondition();
            System.Workflow.Activities.CodeCondition codecondition6 = new System.Workflow.Activities.CodeCondition();
            // 
            // codeStart
            // 
            this.codeStart.Name = "codeStart";
            this.codeStart.ExecuteCode += new System.EventHandler(this.OnStart);
            // 
            // callReceived
            // 
            this.callReceived.InterfaceType = typeof(Southridge.Clinic.Workflow.IReferralActions);
            this.callReceived.MethodName = "Received";
            this.callReceived.Name = "callReceived";
            this.callReceived.ParameterBindings.Add(Bind("referralId", "ReferralId"));
            this.callReceived.ParameterBindings.Add(Bind("instanceId", "CurrentInstanceId"));
            // 
            // handleApproved
            // 
            this.handleApproved.EventName = "Approved";
            this.handleApproved.InterfaceType = typeof(Southridge.Clinic.Workflow.IReferralEvents);
            this.handleApproved.Name = "handleApproved";
            this.handleApproved.Invoked += new System.EventHandler<System.Workflow.Activities.ExternalDataEventArgs>(this.OnApproved);
            // 
            // eventApproved
            // 
            this.eventApproved.Activities.Add(this.handleApproved);
            this.eventApproved.Name = "eventApproved";
            // 
            // handleDeclined
            // 
            this.handleDeclined.EventName = "Declined";
            this.handleDeclined.InterfaceType = typeof(Southridge.Clinic.Workflow.IReferralEvents);
            this.handleDeclined.Name = "handleDeclined";
            this.handleDeclined.Invoked += new System.EventHandler<System.Workflow.Activities.ExternalDataEventArgs>(this.OnDeclined);
            // 
            // eventDeclined
            // 
            this.eventDeclined.Activities.Add(this.handleDeclined);
            this.eventDeclined.Name = "eventDeclined";
            // 
            // handleCancelled
            // 
            this.handleCancelled.EventName = "Cancelled";
            this.handleCancelled.InterfaceType = typeof(Southridge.Clinic.Workflow.IReferralEvents);
            this.handleCancelled.Name = "handleCancelled";
            this.handleCancelled.Invoked += new System.EventHandler<System.Workflow.Activities.ExternalDataEventArgs>(this.OnCancelled);
            // 
            // eventCancelled
            // 
            this.eventCancelled.Activities.Add(this.handleCancelled);
            this.eventCancelled.Name = "eventCancelled";
            // 
            // delayReview
            // 
            this.delayReview.Name = "delayReview";
            this.delayReview.TimeoutDuration = System.TimeSpan.Parse("3.00:00:00");
            this.delayReview.InitializeTimeoutDuration += new System.EventHandler(this.SetReviewTimeout);
            // 
            // codeReviewTimeout
            // 
            this.codeReviewTimeout.Name = "codeReviewTimeout";
            this.codeReviewTimeout.ExecuteCode += new System.EventHandler(this.OnReviewTimeout);
            // 
            // callEscalate
            // 
            this.callEscalate.InterfaceType = typeof(Southridge.Clinic.Workflow.IReferralActions);
            this.callEscalate.MethodName = "Escalate";
            this.callEscalate.Name = "callEscalate";
            this.callEscalate.ParameterBindings.Add(Bind("referralId", "ReferralId"));
            this.callEscalate.ParameterBindings.Add(Bind("escalation", "EscalationCount"));
            this.callEscalate.ParameterBindings.Add(Bind("reason", "EscalationReason"));
            // 
            // codeAfterEscalation
            // 
            this.codeAfterEscalation.Name = "codeAfterEscalation";
            this.codeAfterEscalation.ExecuteCode += new System.EventHandler(this.AfterEscalation);
            // 
            // eventReviewTimeout
            // 
            this.eventReviewTimeout.Activities.Add(this.delayReview);
            this.eventReviewTimeout.Activities.Add(this.codeReviewTimeout);
            this.eventReviewTimeout.Activities.Add(this.callEscalate);
            this.eventReviewTimeout.Activities.Add(this.codeAfterEscalation);
            this.eventReviewTimeout.Name = "eventReviewTimeout";
            // 
            // listenDecision
            // 
            this.listenDecision.Activities.Add(this.eventApproved);
            this.listenDecision.Activities.Add(this.eventDeclined);
            this.listenDecision.Activities.Add(this.eventCancelled);
            this.listenDecision.Activities.Add(this.eventReviewTimeout);
            this.listenDecision.Name = "listenDecision";
            // 
            // whileAwaitingDecision
            // 
            this.whileAwaitingDecision.Activities.Add(this.listenDecision);
            codecondition1.Condition += new System.EventHandler<System.Workflow.Activities.ConditionalEventArgs>(this.AwaitingDecision);
            this.whileAwaitingDecision.Condition = codecondition1;
            this.whileAwaitingDecision.Name = "whileAwaitingDecision";
            // 
            // callApprovedLetter
            // 
            this.callApprovedLetter.InterfaceType = typeof(Southridge.Clinic.Workflow.IReferralActions);
            this.callApprovedLetter.MethodName = "SendDecisionLetter";
            this.callApprovedLetter.Name = "callApprovedLetter";
            this.callApprovedLetter.ParameterBindings.Add(Bind("referralId", "ReferralId"));
            this.callApprovedLetter.ParameterBindings.Add(Bind("approved", "DecisionApproved"));
            // 
            // handleScheduled
            // 
            this.handleScheduled.EventName = "Scheduled";
            this.handleScheduled.InterfaceType = typeof(Southridge.Clinic.Workflow.IReferralEvents);
            this.handleScheduled.Name = "handleScheduled";
            this.handleScheduled.Invoked += new System.EventHandler<System.Workflow.Activities.ExternalDataEventArgs>(this.OnScheduled);
            // 
            // eventScheduled
            // 
            this.eventScheduled.Activities.Add(this.handleScheduled);
            this.eventScheduled.Name = "eventScheduled";
            // 
            // handleScheduleCancelled
            // 
            this.handleScheduleCancelled.EventName = "Cancelled";
            this.handleScheduleCancelled.InterfaceType = typeof(Southridge.Clinic.Workflow.IReferralEvents);
            this.handleScheduleCancelled.Name = "handleScheduleCancelled";
            this.handleScheduleCancelled.Invoked += new System.EventHandler<System.Workflow.Activities.ExternalDataEventArgs>(this.OnScheduleCancelled);
            // 
            // eventScheduleCancelled
            // 
            this.eventScheduleCancelled.Activities.Add(this.handleScheduleCancelled);
            this.eventScheduleCancelled.Name = "eventScheduleCancelled";
            // 
            // delayScheduling
            // 
            this.delayScheduling.Name = "delayScheduling";
            this.delayScheduling.TimeoutDuration = System.TimeSpan.Parse("14.00:00:00");
            // 
            // codeSchedulingTimeout
            // 
            this.codeSchedulingTimeout.Name = "codeSchedulingTimeout";
            this.codeSchedulingTimeout.ExecuteCode += new System.EventHandler(this.OnSchedulingTimeout);
            // 
            // callSchedulingEscalate
            // 
            this.callSchedulingEscalate.InterfaceType = typeof(Southridge.Clinic.Workflow.IReferralActions);
            this.callSchedulingEscalate.MethodName = "Escalate";
            this.callSchedulingEscalate.Name = "callSchedulingEscalate";
            this.callSchedulingEscalate.ParameterBindings.Add(Bind("referralId", "ReferralId"));
            this.callSchedulingEscalate.ParameterBindings.Add(Bind("escalation", "EscalationCount"));
            this.callSchedulingEscalate.ParameterBindings.Add(Bind("reason", "EscalationReason"));
            // 
            // eventSchedulingTimeout
            // 
            this.eventSchedulingTimeout.Activities.Add(this.delayScheduling);
            this.eventSchedulingTimeout.Activities.Add(this.codeSchedulingTimeout);
            this.eventSchedulingTimeout.Activities.Add(this.callSchedulingEscalate);
            this.eventSchedulingTimeout.Name = "eventSchedulingTimeout";
            // 
            // listenAppointment
            // 
            this.listenAppointment.Activities.Add(this.eventScheduled);
            this.listenAppointment.Activities.Add(this.eventScheduleCancelled);
            this.listenAppointment.Activities.Add(this.eventSchedulingTimeout);
            this.listenAppointment.Name = "listenAppointment";
            // 
            // whileAwaitingAppointment
            // 
            this.whileAwaitingAppointment.Activities.Add(this.listenAppointment);
            codecondition2.Condition += new System.EventHandler<System.Workflow.Activities.ConditionalEventArgs>(this.AwaitingAppointment);
            this.whileAwaitingAppointment.Condition = codecondition2;
            this.whileAwaitingAppointment.Name = "whileAwaitingAppointment";
            // 
            // branchApproved
            // 
            this.branchApproved.Activities.Add(this.callApprovedLetter);
            this.branchApproved.Activities.Add(this.whileAwaitingAppointment);
            codecondition3.Condition += new System.EventHandler<System.Workflow.Activities.ConditionalEventArgs>(this.IsApproved);
            this.branchApproved.Condition = codecondition3;
            this.branchApproved.Name = "branchApproved";
            // 
            // callDeclinedLetter
            // 
            this.callDeclinedLetter.InterfaceType = typeof(Southridge.Clinic.Workflow.IReferralActions);
            this.callDeclinedLetter.MethodName = "SendDecisionLetter";
            this.callDeclinedLetter.Name = "callDeclinedLetter";
            this.callDeclinedLetter.ParameterBindings.Add(Bind("referralId", "ReferralId"));
            this.callDeclinedLetter.ParameterBindings.Add(Bind("approved", "DecisionApproved"));
            // 
            // branchDeclined
            // 
            this.branchDeclined.Activities.Add(this.callDeclinedLetter);
            codecondition4.Condition += new System.EventHandler<System.Workflow.Activities.ConditionalEventArgs>(this.IsDeclined);
            this.branchDeclined.Condition = codecondition4;
            this.branchDeclined.Name = "branchDeclined";
            // 
            // callExpire
            // 
            this.callExpire.InterfaceType = typeof(Southridge.Clinic.Workflow.IReferralActions);
            this.callExpire.MethodName = "Expire";
            this.callExpire.Name = "callExpire";
            this.callExpire.ParameterBindings.Add(Bind("referralId", "ReferralId"));
            this.callExpire.ParameterBindings.Add(Bind("reason", "ExpireReason"));
            // 
            // branchExpired
            // 
            this.branchExpired.Activities.Add(this.callExpire);
            codecondition5.Condition += new System.EventHandler<System.Workflow.Activities.ConditionalEventArgs>(this.IsExpired);
            this.branchExpired.Condition = codecondition5;
            this.branchExpired.Name = "branchExpired";
            // 
            // ifDecision
            // 
            this.ifDecision.Activities.Add(this.branchApproved);
            this.ifDecision.Activities.Add(this.branchDeclined);
            this.ifDecision.Activities.Add(this.branchExpired);
            this.ifDecision.Name = "ifDecision";
            // 
            // callExpireUnscheduled
            // 
            this.callExpireUnscheduled.InterfaceType = typeof(Southridge.Clinic.Workflow.IReferralActions);
            this.callExpireUnscheduled.MethodName = "Expire";
            this.callExpireUnscheduled.Name = "callExpireUnscheduled";
            this.callExpireUnscheduled.ParameterBindings.Add(Bind("referralId", "ReferralId"));
            this.callExpireUnscheduled.ParameterBindings.Add(Bind("reason", "ExpireReason"));
            // 
            // branchNotScheduled
            // 
            this.branchNotScheduled.Activities.Add(this.callExpireUnscheduled);
            codecondition6.Condition += new System.EventHandler<System.Workflow.Activities.ConditionalEventArgs>(this.NotScheduled);
            this.branchNotScheduled.Condition = codecondition6;
            this.branchNotScheduled.Name = "branchNotScheduled";
            // 
            // ifNotScheduled
            // 
            this.ifNotScheduled.Activities.Add(this.branchNotScheduled);
            this.ifNotScheduled.Name = "ifNotScheduled";
            // 
            // codeComplete
            // 
            this.codeComplete.Name = "codeComplete";
            this.codeComplete.ExecuteCode += new System.EventHandler(this.OnComplete);
            // 
            // callCompleted
            // 
            this.callCompleted.InterfaceType = typeof(Southridge.Clinic.Workflow.IReferralActions);
            this.callCompleted.MethodName = "Completed";
            this.callCompleted.Name = "callCompleted";
            this.callCompleted.ParameterBindings.Add(Bind("referralId", "ReferralId"));
            this.callCompleted.ParameterBindings.Add(Bind("outcome", "Outcome"));
            // 
            // ReferralApprovalWorkflow
            // 
            this.Activities.Add(this.codeStart);
            this.Activities.Add(this.callReceived);
            this.Activities.Add(this.whileAwaitingDecision);
            this.Activities.Add(this.ifDecision);
            this.Activities.Add(this.ifNotScheduled);
            this.Activities.Add(this.codeComplete);
            this.Activities.Add(this.callCompleted);
            this.Name = "ReferralApprovalWorkflow";
            this.CanModifyActivities = false;
        }

        private static System.Workflow.ComponentModel.WorkflowParameterBinding Bind(string parameterName, string path)
        {
            System.Workflow.ComponentModel.ActivityBind activitybind = new System.Workflow.ComponentModel.ActivityBind();
            activitybind.Name = "ReferralApprovalWorkflow";
            activitybind.Path = path;
            System.Workflow.ComponentModel.WorkflowParameterBinding binding = new System.Workflow.ComponentModel.WorkflowParameterBinding();
            binding.ParameterName = parameterName;
            binding.SetBinding(System.Workflow.ComponentModel.WorkflowParameterBinding.ValueProperty, activitybind);
            return binding;
        }

        #endregion

        private CodeActivity codeStart;
        private CallExternalMethodActivity callReceived;
        private WhileActivity whileAwaitingDecision;
        private ListenActivity listenDecision;
        private EventDrivenActivity eventApproved;
        private HandleExternalEventActivity handleApproved;
        private EventDrivenActivity eventDeclined;
        private HandleExternalEventActivity handleDeclined;
        private EventDrivenActivity eventCancelled;
        private HandleExternalEventActivity handleCancelled;
        private EventDrivenActivity eventReviewTimeout;
        private DelayActivity delayReview;
        private CodeActivity codeReviewTimeout;
        private CallExternalMethodActivity callEscalate;
        private CodeActivity codeAfterEscalation;
        private IfElseActivity ifDecision;
        private IfElseBranchActivity branchApproved;
        private CallExternalMethodActivity callApprovedLetter;
        private WhileActivity whileAwaitingAppointment;
        private ListenActivity listenAppointment;
        private EventDrivenActivity eventScheduled;
        private HandleExternalEventActivity handleScheduled;
        private EventDrivenActivity eventScheduleCancelled;
        private HandleExternalEventActivity handleScheduleCancelled;
        private EventDrivenActivity eventSchedulingTimeout;
        private DelayActivity delayScheduling;
        private CodeActivity codeSchedulingTimeout;
        private CallExternalMethodActivity callSchedulingEscalate;
        private IfElseBranchActivity branchDeclined;
        private CallExternalMethodActivity callDeclinedLetter;
        private IfElseBranchActivity branchExpired;
        private CallExternalMethodActivity callExpire;
        private IfElseActivity ifNotScheduled;
        private IfElseBranchActivity branchNotScheduled;
        private CallExternalMethodActivity callExpireUnscheduled;
        private CodeActivity codeComplete;
        private CallExternalMethodActivity callCompleted;
    }
}
