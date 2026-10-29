using System;
using System.Activities;
using System.Collections.Generic;
using System.Threading.Tasks;
using StaffOnboarding.Core.Requests;
using StaffOnboarding.Workflows.Services;

namespace StaffOnboarding.Workflows.Activities
{
    public sealed class CreateApprovalTask : CodeActivity<Guid>
    {
        [RequiredArgument] public InArgument<string> RequestNumber { get; set; }
        [RequiredArgument] public InArgument<TaskKind> Kind { get; set; }
        [RequiredArgument] public InArgument<string> AssignedTo { get; set; }
        [RequiredArgument] public InArgument<string> Subject { get; set; }
        [RequiredArgument] public InArgument<ProcessSettings> Settings { get; set; }

        protected override Guid Execute(CodeActivityContext context)
        {
            var settings = Settings.Get(context);
            var number = RequestNumber.Get(context);
            var assignee = AssignedTo.Get(context);
            var kind = Kind.Get(context);
            var taskId = Guid.NewGuid();
            var due = DateTime.UtcNow.Add(settings.ReminderInterval);

            context.GetExtension<IOnboardingStore>().CreateTask(taskId, number, kind, assignee, due);
            context.GetExtension<IOnboardingStore>().AddTimeline(number, "workflow", "Task created", kind + " for " + assignee);

            Mail(context, number, "task-assigned", assignee, new Dictionary<string, string>
            {
                { "Subject", Subject.Get(context) },
                { "TaskKind", Describe(kind) },
                { "TaskUrl", settings.TaskUrl(taskId) },
                { "DueDate", due.ToLocalTime().ToString("dd.MM.yyyy") }
            });
            return taskId;
        }

        internal static string Describe(TaskKind kind)
        {
            switch (kind)
            {
                case TaskKind.ManagerApproval: return "Manager approval";
                case TaskKind.ITApproval: return "IT security approval";
                case TaskKind.HomeFolderHandover: return "Home folder handover";
                default: return kind.ToString();
            }
        }

        internal static void Mail(ActivityContext context, string number, string template, string recipient, Dictionary<string, string> values)
        {
            values["RequestNumber"] = number;
            try
            {
                context.GetExtension<INotifier>().Send(template, new[] { recipient }, values);
            }
            catch (Exception ex)
            {
                context.GetExtension<IOnboardingStore>().AddTimeline(number, "workflow", "Mail failed", template + ": " + ex.GetBaseException().Message);
            }
        }
    }

    public sealed class CloseApprovalTask : CodeActivity
    {
        [RequiredArgument] public InArgument<Guid> TaskId { get; set; }
        [RequiredArgument] public InArgument<string> Status { get; set; }
        public InArgument<ApprovalDecision> Decision { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var decision = Decision.Get(context);
            context.GetExtension<IOnboardingStore>().CloseTask(
                TaskId.Get(context),
                Status.Get(context),
                decision == null ? (DecisionOutcome?)null : decision.Outcome,
                decision == null ? "workflow" : decision.DecidedBy,
                decision == null ? null : decision.Comment);
        }
    }

    public sealed class SendTaskReminder : CodeActivity
    {
        [RequiredArgument] public InArgument<Guid> TaskId { get; set; }
        [RequiredArgument] public InArgument<string> RequestNumber { get; set; }
        [RequiredArgument] public InArgument<string> AssignedTo { get; set; }
        [RequiredArgument] public InArgument<string> Subject { get; set; }
        [RequiredArgument] public InArgument<int> ReminderNumber { get; set; }
        [RequiredArgument] public InArgument<ProcessSettings> Settings { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var taskId = TaskId.Get(context);
            var number = RequestNumber.Get(context);
            context.GetExtension<IOnboardingStore>().CountReminder(taskId);
            CreateApprovalTask.Mail(context, number, "task-reminder", AssignedTo.Get(context), new Dictionary<string, string>
            {
                { "Subject", Subject.Get(context) },
                { "TaskUrl", Settings.Get(context).TaskUrl(taskId) },
                { "ReminderNumber", ReminderNumber.Get(context).ToString() }
            });
        }
    }

    /// <summary>
    /// Finds who an overdue task goes to next: the assignee's own manager from the directory, or the
    /// escalation group for IT approvals. Falls back to HR.
    /// </summary>
    public sealed class ResolveEscalation : AsyncCodeActivity<string>
    {
        [RequiredArgument] public InArgument<string> Assignee { get; set; }
        [RequiredArgument] public InArgument<ProcessSettings> Settings { get; set; }

        protected override IAsyncResult BeginExecute(AsyncCodeActivityContext context, AsyncCallback callback, object state)
        {
            var assignee = Assignee.Get(context);
            var settings = Settings.Get(context);
            var directory = context.GetExtension<IProvisioningClient>();
            var task = Resolve(directory, assignee, settings);
            return AsyncResultAdapter.Wrap(task, callback, state);
        }

        protected override string EndExecute(AsyncCodeActivityContext context, IAsyncResult result)
        {
            return ((Task<string>)result).GetAwaiter().GetResult();
        }

        private static async Task<string> Resolve(IProvisioningClient directory, string assignee, ProcessSettings settings)
        {
            if (string.Equals(assignee, settings.ITApprovers, StringComparison.OrdinalIgnoreCase))
                return settings.ITEscalation;
            if (assignee.StartsWith("group:", StringComparison.OrdinalIgnoreCase))
                return settings.HRTeam;

            var account = await directory.GetAccountAsync(assignee).ConfigureAwait(false);
            if (account == null || string.IsNullOrEmpty(account.ManagerAccount) ||
                string.Equals(account.ManagerAccount, assignee, StringComparison.OrdinalIgnoreCase))
                return settings.HRTeam;
            return account.ManagerAccount;
        }
    }

    internal static class AsyncResultAdapter
    {
        /// <summary>Adapts a Task to the Begin/End pattern used by AsyncCodeActivity.</summary>
        public static IAsyncResult Wrap<T>(Task<T> task, AsyncCallback callback, object state)
        {
            var tcs = new TaskCompletionSource<T>(state);
            task.ContinueWith(t =>
            {
                if (t.IsFaulted) tcs.TrySetException(t.Exception.InnerExceptions);
                else if (t.IsCanceled) tcs.TrySetCanceled();
                else tcs.TrySetResult(t.Result);
                if (callback != null) callback(tcs.Task);
            }, TaskScheduler.Default);
            return tcs.Task;
        }
    }
}
