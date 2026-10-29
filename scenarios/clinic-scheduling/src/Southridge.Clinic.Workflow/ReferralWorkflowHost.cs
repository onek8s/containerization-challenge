using System;
using System.Collections.Generic;
using System.Workflow.Activities;
using System.Workflow.Runtime;
using System.Workflow.Runtime.Hosting;

namespace Southridge.Clinic.Workflow
{
    /// <summary>
    /// Hosts the WF 3.5 runtime for referrals with SqlWorkflowPersistenceService (SouthridgeWorkflow database).
    /// One instance per process; the services host creates it in Application_Start.
    /// </summary>
    public sealed class ReferralWorkflowHost : IDisposable
    {
        private readonly WorkflowRuntime _runtime;
        private readonly ReferralEventService _events = new ReferralEventService();

        public ReferralWorkflowHost(string persistenceConnectionString, IReferralActions actions)
            : this(persistenceConnectionString, actions, TimeSpan.FromMinutes(2))
        {
        }

        public ReferralWorkflowHost(string persistenceConnectionString, IReferralActions actions, TimeSpan loadingInterval)
        {
            _runtime = new WorkflowRuntime("Referrals");

            // Unload idle instances so a coordinator decision days later reloads them from SQL.
            var persistence = new SqlWorkflowPersistenceService(persistenceConnectionString, true, TimeSpan.FromMinutes(5), loadingInterval);
            _runtime.AddService(persistence);

            var exchange = new ExternalDataExchangeService();
            _runtime.AddService(exchange);
            exchange.AddService(_events);
            exchange.AddService(actions);

            _runtime.WorkflowTerminated += delegate(object sender, WorkflowTerminatedEventArgs e)
            {
                EventHandler<WorkflowFailedEventArgs> handler = Failed;
                if (handler != null)
                {
                    handler(this, new WorkflowFailedEventArgs(e.WorkflowInstance.InstanceId, e.Exception));
                }
            };
        }

        public event EventHandler<WorkflowFailedEventArgs> Failed;

        public ReferralEventService Events
        {
            get { return _events; }
        }

        public void Start()
        {
            _runtime.StartRuntime();
        }

        public Guid StartReferral(int referralId, int priority)
        {
            var parameters = new Dictionary<string, object>();
            parameters.Add("ReferralId", referralId);
            parameters.Add("Priority", priority);
            WorkflowInstance instance = _runtime.CreateWorkflow(typeof(ReferralApprovalWorkflow), parameters);
            instance.Start();
            return instance.InstanceId;
        }

        public void Approve(Guid instanceId, string user, string note)
        {
            _events.RaiseApproved(instanceId, user, note);
        }

        public void Decline(Guid instanceId, string user, string note)
        {
            _events.RaiseDeclined(instanceId, user, note);
        }

        public void Cancel(Guid instanceId, string user, string note)
        {
            _events.RaiseCancelled(instanceId, user, note);
        }

        public void Scheduled(Guid instanceId, int appointmentId)
        {
            _events.RaiseScheduled(instanceId, appointmentId);
        }

        public void Dispose()
        {
            if (_runtime.IsStarted)
            {
                _runtime.StopRuntime();
            }

            _runtime.Dispose();
        }
    }

    public class WorkflowFailedEventArgs : EventArgs
    {
        public WorkflowFailedEventArgs(Guid instanceId, Exception exception)
        {
            InstanceId = instanceId;
            Exception = exception;
        }

        public Guid InstanceId { get; private set; }

        public Exception Exception { get; private set; }
    }
}
