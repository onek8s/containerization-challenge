using System;
using System.Activities.Tracking;
using System.Diagnostics;
using StaffOnboarding.Workflows.Activities;

namespace StaffOnboarding.WorkflowHost
{
    /// <summary>Writes suspended, aborted and faulted workflow instances to the Application event log.</summary>
    public class EventLogTracking : TrackingParticipant
    {
        private readonly string _service;

        public EventLogTracking(string service)
        {
            _service = service;
            TrackingProfile = new TrackingProfile
            {
                Name = "StaffOnboardingOperations",
                Queries =
                {
                    new WorkflowInstanceQuery
                    {
                        States =
                        {
                            WorkflowInstanceStates.Suspended,
                            WorkflowInstanceStates.Aborted,
                            WorkflowInstanceStates.Terminated,
                            WorkflowInstanceStates.UnhandledException
                        }
                    },
                    new FaultPropagationQuery { FaultSourceActivityName = "*" }
                }
            };
        }

        protected override void Track(TrackingRecord record, TimeSpan timeout)
        {
            var text = _service + " instance " + record.InstanceId + ": ";
            var unhandled = record as WorkflowInstanceUnhandledExceptionRecord;
            if (unhandled != null)
            {
                Write(text + "unhandled exception in " + unhandled.FaultSource.Name + ": " + unhandled.UnhandledException, EventLogEntryType.Error, 3301);
                return;
            }

            var suspended = record as WorkflowInstanceSuspendedRecord;
            if (suspended != null)
            {
                Write(text + "suspended: " + suspended.Reason, EventLogEntryType.Warning, 3302);
                return;
            }

            var aborted = record as WorkflowInstanceAbortedRecord;
            if (aborted != null)
            {
                Write(text + "aborted: " + aborted.Reason, EventLogEntryType.Warning, 3303);
                return;
            }

            var terminated = record as WorkflowInstanceTerminatedRecord;
            if (terminated != null)
            {
                Write(text + "terminated: " + terminated.Reason, EventLogEntryType.Warning, 3304);
                return;
            }

            var fault = record as FaultPropagationRecord;
            if (fault != null && fault.Fault != null)
                Write(text + "fault in " + fault.FaultSource.Name + ": " + fault.Fault.Message, EventLogEntryType.Warning, 3305);
        }

        private static void Write(string message, EventLogEntryType type, int id)
        {
            try
            {
                EventLog.WriteEntry(HostLog.Source, message, type, id);
            }
            catch (Exception)
            {
                // The event log can be full or the source missing on a fresh server; tracking must not stop the workflow.
            }
        }
    }
}
