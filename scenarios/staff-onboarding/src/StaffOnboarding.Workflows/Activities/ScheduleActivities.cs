using System;
using System.Activities;
using StaffOnboarding.Workflows.Services;

namespace StaffOnboarding.Workflows.Activities
{
    public enum Milestone
    {
        JoinerProvisioning,
        JoinerEnable,
        MoverApply,
        MoverRemoveOldAccess,
        LeaverDisable,
        LeaverCleanup
    }

    /// <summary>Returns the local office time when a milestone is due.</summary>
    public sealed class GetDueTime : CodeActivity<DateTime>
    {
        [RequiredArgument] public InArgument<Milestone> Milestone { get; set; }
        [RequiredArgument] public InArgument<DateTime> Date { get; set; }
        public InArgument<int> Days { get; set; }

        protected override DateTime Execute(CodeActivityContext context)
        {
            var schedule = context.GetExtension<OnboardingPolicy>().Schedule;
            var date = Date.Get(context);
            switch (Milestone.Get(context))
            {
                case Activities.Milestone.JoinerProvisioning: return schedule.JoinerProvisioningTime(date);
                case Activities.Milestone.JoinerEnable: return schedule.JoinerEnableTime(date);
                case Activities.Milestone.MoverApply: return schedule.MoverApplyTime(date);
                case Activities.Milestone.MoverRemoveOldAccess: return schedule.MoverRemoveOldAccessTime(date, Days.Get(context));
                case Activities.Milestone.LeaverDisable: return schedule.LeaverDisableTime(date);
                case Activities.Milestone.LeaverCleanup: return schedule.LeaverCleanupTime(date);
                default: throw new ArgumentOutOfRangeException("Milestone");
            }
        }
    }

    /// <summary>Delay until a local due time. Also publishes the next step for the portal and operators.</summary>
    public sealed class GetDelay : CodeActivity<TimeSpan>
    {
        [RequiredArgument] public InArgument<string> RequestNumber { get; set; }
        [RequiredArgument] public InArgument<DateTime> DueLocal { get; set; }
        [RequiredArgument] public InArgument<string> Step { get; set; }

        protected override TimeSpan Execute(CodeActivityContext context)
        {
            var schedule = context.GetExtension<OnboardingPolicy>().Schedule;
            var due = DueLocal.Get(context);
            var dueUtc = schedule.ToUtc(due);
            context.GetExtension<IOnboardingStore>().SetSchedule(RequestNumber.Get(context), dueUtc, Step.Get(context));

            var promotion = context.GetExtension<RequestPromotion>();
            if (promotion != null) promotion.NextStepAtUtc = dueUtc;

            return schedule.DelayUntil(due, DateTime.UtcNow);
        }
    }

    public sealed class ClearSchedule : CodeActivity
    {
        [RequiredArgument] public InArgument<string> RequestNumber { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            context.GetExtension<IOnboardingStore>().SetSchedule(RequestNumber.Get(context), null, null);
            var promotion = context.GetExtension<RequestPromotion>();
            if (promotion != null) promotion.NextStepAtUtc = null;
        }
    }
}
