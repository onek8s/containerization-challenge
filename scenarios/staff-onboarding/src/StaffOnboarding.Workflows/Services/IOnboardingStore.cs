using System;
using StaffOnboarding.Core.Requests;

namespace StaffOnboarding.Workflows.Services
{
    /// <summary>Onboarding tables (schema onb) that the portal reads. Workflow state itself lives in the instance store.</summary>
    public interface IOnboardingStore
    {
        void AttachInstance(string requestNumber, Guid instanceId, string definition);
        void SetStatus(string requestNumber, RequestStatus status);
        void SetSchedule(string requestNumber, DateTime? nextStepAtUtc, string nextStep);
        void AddTimeline(string requestNumber, string actor, string eventName, string detail);
        void CreateTask(Guid taskId, string requestNumber, TaskKind kind, string assignedTo, DateTime dueUtc);
        void CloseTask(Guid taskId, string status, DecisionOutcome? outcome, string decidedBy, string comment);
        void CountReminder(Guid taskId);
        void CloseOpenTasks(string requestNumber, string status);
        void SaveAccount(string requestNumber, string samAccountName, string userPrincipalName, string mail, string distinguishedName, string homeDirectory);
        void SetSubjectAccount(string requestNumber, string samAccountName);
    }
}
