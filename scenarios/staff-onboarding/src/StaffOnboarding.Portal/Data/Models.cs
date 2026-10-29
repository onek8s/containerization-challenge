using StaffOnboarding.Core.Requests;

namespace StaffOnboarding.Portal.Data;

public sealed record RequestRow(
    string RequestNumber,
    RequestKind Kind,
    RequestStatus Status,
    string Subject,
    string? SamAccountName,
    string? Department,
    string? ManagerAccount,
    DateTime EffectiveDate,
    string RequestedBy,
    DateTime SubmittedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? NextStepAtUtc,
    string? NextStep,
    Guid? WorkflowInstanceId,
    string? Definition,
    string Payload);

public sealed record TaskRow(
    Guid TaskId,
    string RequestNumber,
    TaskKind Kind,
    string AssignedTo,
    DateTime CreatedAtUtc,
    DateTime DueAtUtc,
    string Status,
    DecisionOutcome? Outcome,
    string? DecidedBy,
    DateTime? DecidedAtUtc,
    string? Comment,
    int Reminders,
    string Subject,
    RequestKind RequestKind);

public sealed record TimelineRow(DateTime AtUtc, string Actor, string Event, string? Detail);

public sealed record AccountRow(string SamAccountName, string? UserPrincipalName, string? Mail, string? DistinguishedName, string? HomeDirectory, DateTime CreatedAtUtc);
