using System;
using System.Activities;
using StaffOnboarding.Core.Requests;
using StaffOnboarding.Workflows.Services;

namespace StaffOnboarding.Workflows.Activities
{
    public sealed class ApplyStartDate : CodeActivity
    {
        [RequiredArgument] public InArgument<JoinerRequest> Request { get; set; }
        [RequiredArgument] public InArgument<DateTime> StartDate { get; set; }
        public InArgument<string> ChangedBy { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var request = Request.Get(context);
            var old = request.StartDate;
            request.StartDate = StartDate.Get(context).Date;
            context.GetExtension<IOnboardingStore>().AddTimeline(request.RequestNumber, ChangedBy.Get(context), "Start date changed",
                old.ToString("dd.MM.yyyy") + " -> " + request.StartDate.ToString("dd.MM.yyyy"));
        }
    }

    public sealed class ApplyLastWorkingDay : CodeActivity
    {
        [RequiredArgument] public InArgument<LeaverRequest> Request { get; set; }
        [RequiredArgument] public InArgument<DateTime> LastWorkingDay { get; set; }
        public InArgument<string> ChangedBy { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var request = Request.Get(context);
            var old = request.LastWorkingDay;
            request.LastWorkingDay = LastWorkingDay.Get(context).Date;
            context.GetExtension<IOnboardingStore>().AddTimeline(request.RequestNumber, ChangedBy.Get(context), "Last working day changed",
                old.ToString("dd.MM.yyyy") + " -> " + request.LastWorkingDay.ToString("dd.MM.yyyy"));
        }
    }
}
