using System;
using System.Activities;
using System.Linq;
using StaffOnboarding.Core.Access;
using StaffOnboarding.Core.Provisioning;
using StaffOnboarding.Core.Requests;
using StaffOnboarding.Workflows.Services;

namespace StaffOnboarding.Workflows.Activities
{
    public sealed class PlanJoinerAccess : CodeActivity<AccessPlan>
    {
        [RequiredArgument] public InArgument<JoinerRequest> Request { get; set; }

        protected override AccessPlan Execute(CodeActivityContext context)
        {
            var request = Request.Get(context);
            var plan = context.GetExtension<OnboardingPolicy>().Planner.PlanJoiner(request.Position, request.AdditionalGroups);
            Record(context, request.RequestNumber, plan);
            return plan;
        }

        internal static void Record(CodeActivityContext context, string requestNumber, AccessPlan plan)
        {
            var store = context.GetExtension<IOnboardingStore>();
            var detail = "Add: " + string.Join(", ", plan.AddGroups) +
                         (plan.RemoveGroups.Count > 0 ? "; remove: " + string.Join(", ", plan.RemoveGroups) : "") +
                         (plan.DeferredRemoveGroups.Count > 0 ? "; remove after handover: " + string.Join(", ", plan.DeferredRemoveGroups) : "") +
                         "; OU: " + plan.TargetOu;
            store.AddTimeline(requestNumber, "workflow", "Access planned", detail);
            foreach (var warning in plan.Warnings)
                store.AddTimeline(requestNumber, "workflow", "Access warning", warning);
        }
    }

    public sealed class PlanMoverAccess : CodeActivity<AccessPlan>
    {
        [RequiredArgument] public InArgument<MoverRequest> Request { get; set; }
        [RequiredArgument] public InArgument<AccountSummary> Account { get; set; }

        protected override AccessPlan Execute(CodeActivityContext context)
        {
            var request = Request.Get(context);
            var account = Account.Get(context);
            var plan = context.GetExtension<OnboardingPolicy>().Planner.PlanMover(
                account.Groups, request.NewPosition, request.AdditionalGroups, request.HandoverDays);
            PlanJoinerAccess.Record(context, request.RequestNumber, plan);
            return plan;
        }
    }

    public sealed class PlanLeaverAccess : CodeActivity<AccessPlan>
    {
        [RequiredArgument] public InArgument<string> RequestNumber { get; set; }
        [RequiredArgument] public InArgument<AccountSummary> Account { get; set; }

        protected override AccessPlan Execute(CodeActivityContext context)
        {
            var plan = context.GetExtension<OnboardingPolicy>().Planner.PlanLeaver(Account.Get(context).Groups);
            PlanJoinerAccess.Record(context, RequestNumber.Get(context), plan);
            return plan;
        }
    }

    public sealed class BuildCreateAccount : CodeActivity<CreateAccountCommand>
    {
        [RequiredArgument] public InArgument<JoinerRequest> Request { get; set; }
        [RequiredArgument] public InArgument<AccessPlan> Plan { get; set; }

        protected override CreateAccountCommand Execute(CodeActivityContext context)
        {
            var r = Request.Get(context);
            var plan = Plan.Get(context);
            var schedule = context.GetExtension<OnboardingPolicy>().Schedule;
            var expiry = schedule.AccountExpiry(r.Position.ContractEndDate);

            return new CreateAccountCommand
            {
                RequestNumber = r.RequestNumber,
                Step = "create",
                FirstName = r.Person.FirstName,
                LastName = r.Person.LastName,
                PreferredName = r.Person.PreferredName,
                EmployeeId = r.Person.EmployeeId,
                MobilePhone = r.Person.MobilePhone,
                Department = r.Position.Department,
                JobRole = r.Position.JobRole,
                Title = r.Position.Title,
                Office = r.Position.Office,
                CostCenter = r.Position.CostCenter,
                ManagerAccount = r.Position.ManagerAccount,
                EmploymentType = r.Position.EmploymentType.ToString(),
                AccountExpiresUtc = expiry.HasValue ? schedule.ToUtc(expiry.Value) : (DateTime?)null,
                TargetOu = plan.TargetOu,
                Groups = plan.AddGroups.ToList(),
                DepartmentShare = plan.DepartmentShare,
                CreateDisabled = true
            };
        }
    }

    public sealed class BuildMoverUpdate : CodeActivity<UpdateAccessCommand>
    {
        [RequiredArgument] public InArgument<MoverRequest> Request { get; set; }
        [RequiredArgument] public InArgument<AccessPlan> Plan { get; set; }

        /// <summary>False for the first step on the effective date, true for the removal after handover.</summary>
        public InArgument<bool> AfterHandover { get; set; }

        protected override UpdateAccessCommand Execute(CodeActivityContext context)
        {
            var r = Request.Get(context);
            var plan = Plan.Get(context);
            if (AfterHandover.Get(context))
            {
                return new UpdateAccessCommand
                {
                    RequestNumber = r.RequestNumber,
                    Step = "remove-old-access",
                    SamAccountName = r.SamAccountName,
                    RemoveGroups = plan.DeferredRemoveGroups.ToList()
                };
            }

            var schedule = context.GetExtension<OnboardingPolicy>().Schedule;
            var expiry = schedule.AccountExpiry(r.NewPosition.ContractEndDate);
            return new UpdateAccessCommand
            {
                RequestNumber = r.RequestNumber,
                Step = "apply",
                SamAccountName = r.SamAccountName,
                Department = r.NewPosition.Department,
                Title = r.NewPosition.Title,
                Office = r.NewPosition.Office,
                CostCenter = r.NewPosition.CostCenter,
                ManagerAccount = r.NewPosition.ManagerAccount,
                TargetOu = plan.TargetOu,
                AccountExpiresUtc = expiry.HasValue ? schedule.ToUtc(expiry.Value) : (DateTime?)null,
                AddGroups = plan.AddGroups.ToList(),
                RemoveGroups = plan.RemoveGroups.ToList()
            };
        }
    }
}
