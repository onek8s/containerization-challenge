using System;
using System.Activities;
using System.Collections.Generic;
using System.Diagnostics;
using StaffOnboarding.Core.Requests;
using StaffOnboarding.Workflows.Services;

namespace StaffOnboarding.Workflows.Activities
{
    /// <summary>Links the request row to this workflow instance.</summary>
    public sealed class AttachInstance : CodeActivity
    {
        [RequiredArgument] public InArgument<string> RequestNumber { get; set; }
        [RequiredArgument] public InArgument<RequestKind> Kind { get; set; }
        public InArgument<string> Subject { get; set; }
        [RequiredArgument] public InArgument<string> Definition { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var number = RequestNumber.Get(context);
            context.GetExtension<IOnboardingStore>().AttachInstance(number, context.WorkflowInstanceId, Definition.Get(context));

            var promotion = context.GetExtension<RequestPromotion>();
            if (promotion != null)
            {
                promotion.RequestNumber = number;
                promotion.Kind = Kind.Get(context).ToString();
                promotion.Subject = Subject.Get(context);
            }
        }
    }

    public sealed class RecordStatus : CodeActivity
    {
        [RequiredArgument] public InArgument<string> RequestNumber { get; set; }
        [RequiredArgument] public InArgument<RequestStatus> Status { get; set; }
        public InArgument<string> Detail { get; set; }
        public InArgument<string> Actor { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var number = RequestNumber.Get(context);
            var status = Status.Get(context);
            var store = context.GetExtension<IOnboardingStore>();
            store.SetStatus(number, status);
            store.AddTimeline(number, Actor.Get(context) ?? "workflow", "Status: " + status, Detail.Get(context));

            var promotion = context.GetExtension<RequestPromotion>();
            if (promotion != null) promotion.Status = status.ToString();
        }
    }

    public sealed class AddTimeline : CodeActivity
    {
        [RequiredArgument] public InArgument<string> RequestNumber { get; set; }
        [RequiredArgument] public InArgument<string> Event { get; set; }
        public InArgument<string> Detail { get; set; }
        public InArgument<string> Actor { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            context.GetExtension<IOnboardingStore>().AddTimeline(
                RequestNumber.Get(context), Actor.Get(context) ?? "workflow", Event.Get(context), Detail.Get(context));
        }
    }

    public sealed class SetSubjectAccount : CodeActivity
    {
        [RequiredArgument] public InArgument<string> RequestNumber { get; set; }
        [RequiredArgument] public InArgument<string> SamAccountName { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            context.GetExtension<IOnboardingStore>().SetSubjectAccount(RequestNumber.Get(context), SamAccountName.Get(context));
        }
    }

    public sealed class CloseOpenTasks : CodeActivity
    {
        [RequiredArgument] public InArgument<string> RequestNumber { get; set; }
        [RequiredArgument] public InArgument<string> Status { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            context.GetExtension<IOnboardingStore>().CloseOpenTasks(RequestNumber.Get(context), Status.Get(context));
        }
    }

    /// <summary>Captures routing and timings for the lifetime of the instance.</summary>
    public sealed class LoadProcessSettings : CodeActivity<ProcessSettings>
    {
        protected override ProcessSettings Execute(CodeActivityContext context)
        {
            var settings = context.GetExtension<OnboardingPolicy>().Settings;
            return new ProcessSettings
            {
                ITApprovers = settings.ITApprovers,
                ITEscalation = settings.ITEscalation,
                HRTeam = settings.HRTeam,
                ServiceDesk = settings.ServiceDesk,
                PortalUrl = settings.PortalUrl,
                ReminderInterval = settings.Schedule.ApprovalReminderInterval,
                RemindersBeforeEscalation = settings.Schedule.RemindersBeforeEscalation,
                EscalationsBeforeExpiry = settings.Schedule.EscalationsBeforeExpiry,
                RetryDelay = settings.Schedule.ProvisioningRetryDelay,
                MaxAttempts = settings.Schedule.ProvisioningMaxAttempts
            };
        }
    }

    /// <summary>Sends mail; a failure is written to the timeline and does not stop the request.</summary>
    public sealed class Notify : CodeActivity
    {
        [RequiredArgument] public InArgument<string> RequestNumber { get; set; }
        [RequiredArgument] public InArgument<string> Template { get; set; }
        [RequiredArgument] public InArgument<string[]> Recipients { get; set; }
        public InArgument<Dictionary<string, string>> Values { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var number = RequestNumber.Get(context);
            var template = Template.Get(context);
            var values = Values.Get(context) ?? new Dictionary<string, string>();
            values["RequestNumber"] = number;
            try
            {
                context.GetExtension<INotifier>().Send(template, Recipients.Get(context), values);
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(HostLog.Source, "Mail " + template + " for " + number + " failed: " + ex, EventLogEntryType.Warning, 3101);
                context.GetExtension<IOnboardingStore>().AddTimeline(number, "workflow", "Mail failed", template + ": " + ex.GetBaseException().Message);
            }
        }
    }

    public static class HostLog
    {
        public const string Source = "Staff Onboarding Workflow";
        public const string LogName = "Application";
    }
}
