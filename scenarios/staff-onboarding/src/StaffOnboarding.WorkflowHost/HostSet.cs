using System;
using System.Activities.DurableInstancing;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.DurableInstancing;
using System.ServiceModel;
using System.ServiceModel.Activities;
using System.ServiceModel.Activities.Description;
using System.ServiceModel.Description;
using StaffOnboarding.Workflows;
using StaffOnboarding.Workflows.Activities;
using StaffOnboarding.Workflows.Definitions;
using StaffOnboarding.Workflows.Services;

namespace StaffOnboarding.WorkflowHost
{
    /// <summary>One WorkflowServiceHost per process (joiner, mover, leaver), all sharing one instance store.</summary>
    public sealed class HostSet : IDisposable
    {
        private readonly List<WorkflowServiceHost> _hosts = new List<WorkflowServiceHost>();
        private ProvisioningClient _provisioning;

        public void Open()
        {
            var settings = WorkflowSettings.FromConfiguration();
            var policy = new OnboardingPolicy(settings);
            var store = new SqlOnboardingStore(settings.OnboardingConnectionString);
            _provisioning = new ProvisioningClient(settings.ProvisioningUrl, settings.ProvisioningTimeout);
            var notifier = new MailNotifier(settings, _provisioning);

            foreach (var definition in WorkflowCatalog.All())
            {
                var host = new WorkflowServiceHost(definition.Current, new Uri(settings.BaseAddress, definition.Path));
                foreach (var older in definition.Supported)
                    host.SupportedVersions.Add(older);

                host.DurableInstancingOptions.InstanceStore = CreateInstanceStore(settings);

                host.AddServiceEndpoint(definition.ContractName, Bindings.Intranet(), string.Empty);
                host.AddServiceEndpoint(new WorkflowControlEndpoint(
                    new NetNamedPipeBinding(NetNamedPipeSecurityMode.Transport),
                    new EndpointAddress(Bindings.ControlAddress(definition.Path))));

                host.Description.Behaviors.Add(new WorkflowIdleBehavior
                {
                    TimeToPersist = TimeSpan.Zero,
                    TimeToUnload = TimeSpan.FromMinutes(1)
                });
                host.Description.Behaviors.Add(new WorkflowUnhandledExceptionBehavior
                {
                    Action = WorkflowUnhandledExceptionAction.AbandonAndSuspend
                });
                host.Description.Behaviors.Add(new ServiceMetadataBehavior { HttpGetEnabled = true });
                host.Description.Behaviors.Add(new ServiceThrottlingBehavior
                {
                    MaxConcurrentCalls = 64,
                    MaxConcurrentInstances = 500,
                    MaxConcurrentSessions = 64
                });
                host.Authorization.ServiceAuthorizationManager = new CallerAuthorization(settings.AllowedCallers);

                host.WorkflowExtensions.Add(policy);
                host.WorkflowExtensions.Add(store);
                host.WorkflowExtensions.Add(_provisioning);
                host.WorkflowExtensions.Add(notifier);
                host.WorkflowExtensions.Add(new EventLogTracking(definition.Path));
                host.WorkflowExtensions.Add(() => new RequestPromotion());

                host.Faulted += (s, e) => EventLog.WriteEntry(HostLog.Source,
                    "Workflow service host for " + definition.Path + " faulted.", EventLogEntryType.Error, 3002);

                host.Open();
                _hosts.Add(host);
                EventLog.WriteEntry(HostLog.Source, "Listening on " + host.BaseAddresses[0] + " (" +
                    definition.Current.DefinitionIdentity + ")", EventLogEntryType.Information, 3000);
            }
        }

        private static SqlWorkflowInstanceStore CreateInstanceStore(WorkflowSettings settings)
        {
            var store = new SqlWorkflowInstanceStore(settings.InstanceStoreConnectionString)
            {
                InstanceCompletionAction = InstanceCompletionAction.DeleteNothing,
                InstanceLockedExceptionAction = InstanceLockedExceptionAction.AggressiveRetry,
                InstanceEncodingOption = InstanceEncodingOption.GZip,
                HostLockRenewalPeriod = TimeSpan.FromSeconds(30),
                RunnableInstancesDetectionPeriod = TimeSpan.FromSeconds(15)
            };
            store.Promote(RequestPromotion.PromotionName, RequestPromotion.PromotedNames, null);
            return store;
        }

        public void Dispose()
        {
            foreach (var host in _hosts)
            {
                try
                {
                    host.Close(TimeSpan.FromSeconds(30));
                }
                catch (Exception)
                {
                    host.Abort();
                }
            }
            _hosts.Clear();
            if (_provisioning != null) _provisioning.Dispose();
        }
    }
}
