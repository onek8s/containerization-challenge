using System;
using System.Activities;
using System.Collections.Generic;
using System.Threading.Tasks;
using StaffOnboarding.Core.Provisioning;
using StaffOnboarding.Core.Requests;
using StaffOnboarding.Workflows.Services;

namespace StaffOnboarding.Workflows.Activities
{
    /// <summary>Sends one command to the provisioning service. Failures are returned, not thrown.</summary>
    public sealed class Provision : AsyncCodeActivity<ProvisioningResult>
    {
        [RequiredArgument] public InArgument<ProvisioningCommand> Command { get; set; }

        protected override IAsyncResult BeginExecute(AsyncCodeActivityContext context, AsyncCallback callback, object state)
        {
            var client = context.GetExtension<IProvisioningClient>();
            var command = Command.Get(context);
            return AsyncResultAdapter.Wrap(Send(client, command), callback, state);
        }

        protected override ProvisioningResult EndExecute(AsyncCodeActivityContext context, IAsyncResult result)
        {
            ProvisioningResult r;
            try
            {
                r = ((Task<ProvisioningResult>)result).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                r = ProvisioningResult.Fail(ex.GetBaseException().Message, true);
            }

            var command = Command.Get(context);
            var detail = command.Step + (r.AlreadyDone ? " (already done)" : "") +
                         (r.Messages.Count > 0 ? ": " + string.Join(" ", r.Messages) : "");
            context.GetExtension<IOnboardingStore>().AddTimeline(command.RequestNumber, "idm",
                r.Succeeded ? "Provisioning done" : (r.Retryable ? "Provisioning retry" : "Provisioning failed"), detail);
            return r;
        }

        private static Task<ProvisioningResult> Send(IProvisioningClient client, ProvisioningCommand command)
        {
            var create = command as CreateAccountCommand;
            if (create != null) return client.CreateAccountAsync(create);
            var enable = command as EnableAccountCommand;
            if (enable != null) return client.EnableAccountAsync(enable);
            var update = command as UpdateAccessCommand;
            if (update != null) return client.UpdateAccessAsync(update);
            var disable = command as DisableAccountCommand;
            if (disable != null) return client.DisableAccountAsync(disable);
            var archive = command as ArchiveAccountCommand;
            if (archive != null) return client.ArchiveAccountAsync(archive);
            throw new ArgumentException("Unknown command " + command.GetType().Name);
        }
    }

    public sealed class GetAccount : AsyncCodeActivity<AccountSummary>
    {
        [RequiredArgument] public InArgument<string> SamAccountName { get; set; }

        protected override IAsyncResult BeginExecute(AsyncCodeActivityContext context, AsyncCallback callback, object state)
        {
            var client = context.GetExtension<IProvisioningClient>();
            return AsyncResultAdapter.Wrap(client.GetAccountAsync(SamAccountName.Get(context)), callback, state);
        }

        protected override AccountSummary EndExecute(AsyncCodeActivityContext context, IAsyncResult result)
        {
            return ((Task<AccountSummary>)result).GetAwaiter().GetResult();
        }
    }

    public sealed class SaveAccount : CodeActivity
    {
        [RequiredArgument] public InArgument<string> RequestNumber { get; set; }
        [RequiredArgument] public InArgument<ProvisioningResult> Result { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var r = Result.Get(context);
            var number = RequestNumber.Get(context);
            var store = context.GetExtension<IOnboardingStore>();
            store.SaveAccount(number, r.SamAccountName, r.UserPrincipalName, r.Mail, r.DistinguishedName, r.HomeDirectory);
            store.SetSubjectAccount(number, r.SamAccountName);
        }
    }

    /// <summary>Writes the welcome pack to the onboarding share and mails the manager where to find it.</summary>
    public sealed class WriteWelcomePack : CodeActivity
    {
        [RequiredArgument] public InArgument<JoinerRequest> Request { get; set; }
        [RequiredArgument] public InArgument<ProvisioningResult> Account { get; set; }
        [RequiredArgument] public InArgument<ProcessSettings> Settings { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var r = Request.Get(context);
            var account = Account.Get(context);
            var settings = Settings.Get(context);
            var values = new Dictionary<string, string>
            {
                { "RequestNumber", r.RequestNumber },
                { "DisplayName", r.Person.DisplayName },
                { "FirstName", r.Person.DisplayFirstName },
                { "Title", r.Position.Title },
                { "Office", r.Position.Office },
                { "StartDate", r.StartDate.ToString("dd.MM.yyyy") },
                { "SamAccountName", account.SamAccountName },
                { "UserPrincipalName", account.UserPrincipalName },
                { "Mail", account.Mail },
                { "HomeDirectory", account.HomeDirectory },
                { "ServiceDesk", settings.ServiceDesk.Replace("group:", "") },
                { "RequestUrl", settings.RequestUrl(r.RequestNumber) }
            };

            var store = context.GetExtension<IOnboardingStore>();
            var notifier = context.GetExtension<INotifier>();
            try
            {
                values["PackFolder"] = notifier.WriteWelcomePack(r.RequestNumber, values);
                store.AddTimeline(r.RequestNumber, "workflow", "Welcome pack written", values["PackFolder"]);
            }
            catch (Exception ex)
            {
                values["PackFolder"] = "(not available)";
                store.AddTimeline(r.RequestNumber, "workflow", "Welcome pack failed", ex.GetBaseException().Message);
            }

            CreateApprovalTask.Mail(context, r.RequestNumber, "account-ready", r.Position.ManagerAccount, values);
        }
    }

    public sealed class RequestSubject : CodeActivity<string>
    {
        public InArgument<JoinerRequest> Joiner { get; set; }
        public InArgument<string> SamAccountName { get; set; }

        protected override string Execute(CodeActivityContext context)
        {
            var joiner = Joiner.Get(context);
            if (joiner != null) return joiner.Person.DisplayName;
            return SamAccountName.Get(context);
        }
    }
}
