using CityPermits.Domain.Model;

namespace CityPermits.Domain.Workflow;

public sealed record Transition(CaseStatus From, string Action, CaseStatus To, string[] AllowedRoles, bool RequiresNote = false);

public sealed class WorkflowException : Exception
{
    public WorkflowException(string message) : base(message) { }
}

/// <summary>
/// The case life cycle agreed with Planning, Licensing and Parking Services (process maps v7, March 2023).
/// </summary>
public static class CaseWorkflow
{
    public const string Submit = "submit";
    public const string PaymentReceived = "payment-received";
    public const string StartIntake = "start-intake";
    public const string Validate = "validate";
    public const string RequestInformation = "request-information";
    public const string InformationProvided = "information-provided";
    public const string Consult = "consult";
    public const string ConsultationClosed = "consultation-closed";
    public const string Recommend = "recommend";
    public const string Approve = "approve";
    public const string Refuse = "refuse";
    public const string ReturnToOfficer = "return-to-officer";
    public const string Issue = "issue";
    public const string Withdraw = "withdraw";
    public const string Expire = "expire";
    public const string Revoke = "revoke";

    private static readonly string[] Officers = { Roles.CaseOfficer, Roles.Supervisor };
    private static readonly string[] Supervisors = { Roles.Supervisor };
    private static readonly string[] SystemOnly = { Roles.System };
    private static readonly string[] ApplicantOrOfficer = { Roles.Applicant, Roles.CaseOfficer, Roles.Supervisor };

    public static readonly IReadOnlyList<Transition> Transitions = new[]
    {
        new Transition(CaseStatus.Draft, Submit, CaseStatus.AwaitingPayment, ApplicantOrOfficer),
        new Transition(CaseStatus.AwaitingPayment, PaymentReceived, CaseStatus.Submitted, new[] { Roles.System, Roles.Finance }),
        new Transition(CaseStatus.Submitted, StartIntake, CaseStatus.IntakeCheck, Officers),
        new Transition(CaseStatus.IntakeCheck, Validate, CaseStatus.UnderReview, Officers),
        new Transition(CaseStatus.IntakeCheck, RequestInformation, CaseStatus.InformationRequested, Officers, true),
        new Transition(CaseStatus.UnderReview, RequestInformation, CaseStatus.InformationRequested, Officers, true),
        new Transition(CaseStatus.InformationRequested, InformationProvided, CaseStatus.UnderReview, ApplicantOrOfficer),
        new Transition(CaseStatus.UnderReview, Consult, CaseStatus.Consultation, Officers),
        new Transition(CaseStatus.Consultation, ConsultationClosed, CaseStatus.UnderReview, new[] { Roles.CaseOfficer, Roles.Supervisor, Roles.System }),
        new Transition(CaseStatus.UnderReview, Recommend, CaseStatus.DecisionPending, Officers, true),
        new Transition(CaseStatus.DecisionPending, Approve, CaseStatus.Approved, Supervisors),
        new Transition(CaseStatus.DecisionPending, Refuse, CaseStatus.Refused, Supervisors, true),
        new Transition(CaseStatus.DecisionPending, ReturnToOfficer, CaseStatus.UnderReview, Supervisors, true),
        new Transition(CaseStatus.Approved, Issue, CaseStatus.Issued, new[] { Roles.System, Roles.Supervisor, Roles.CaseOfficer }),
        new Transition(CaseStatus.Issued, Expire, CaseStatus.Expired, SystemOnly),
        new Transition(CaseStatus.Issued, Revoke, CaseStatus.Revoked, Supervisors, true),
        new Transition(CaseStatus.Draft, Withdraw, CaseStatus.Withdrawn, ApplicantOrOfficer),
        new Transition(CaseStatus.AwaitingPayment, Withdraw, CaseStatus.Withdrawn, ApplicantOrOfficer),
        new Transition(CaseStatus.Submitted, Withdraw, CaseStatus.Withdrawn, ApplicantOrOfficer),
        new Transition(CaseStatus.IntakeCheck, Withdraw, CaseStatus.Withdrawn, ApplicantOrOfficer),
        new Transition(CaseStatus.InformationRequested, Withdraw, CaseStatus.Withdrawn, ApplicantOrOfficer),
        new Transition(CaseStatus.UnderReview, Withdraw, CaseStatus.Withdrawn, ApplicantOrOfficer),
        new Transition(CaseStatus.Consultation, Withdraw, CaseStatus.Withdrawn, ApplicantOrOfficer),
    };

    public static IEnumerable<Transition> Available(PermitCase c, IEnumerable<string> roles)
    {
        var set = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Transitions.Where(t => t.From == c.Status && t.AllowedRoles.Any(set.Contains) && Guard(c, t) is null);
    }

    /// <summary>Applies an action, records the event and returns the new status.</summary>
    public static CaseStatus Apply(PermitCase c, string action, string actor, IEnumerable<string> roles, string? note, DateTime nowUtc)
    {
        var set = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var t = Transitions.FirstOrDefault(x => x.From == c.Status && x.Action == action)
            ?? throw new WorkflowException($"Action '{action}' is not possible when the case is {c.Status}.");
        if (!t.AllowedRoles.Any(set.Contains))
            throw new WorkflowException($"Action '{action}' requires one of: {string.Join(", ", t.AllowedRoles)}.");
        if (t.RequiresNote && string.IsNullOrWhiteSpace(note))
            throw new WorkflowException($"Action '{action}' requires a note.");
        var guard = Guard(c, t);
        if (guard is not null) throw new WorkflowException(guard);

        var from = c.Status;
        c.Status = t.To;
        switch (t.To)
        {
            case CaseStatus.AwaitingPayment:
                c.SubmittedUtc ??= nowUtc;
                break;
            case CaseStatus.Submitted:
                c.SubmittedUtc ??= nowUtc;
                break;
            case CaseStatus.Approved:
            case CaseStatus.Refused:
                c.DecidedUtc = nowUtc;
                c.DecidedBy = actor;
                c.Outcome = t.To == CaseStatus.Refused ? DecisionOutcome.Refused
                    : c.Conditions.Count > 0 ? DecisionOutcome.ApprovedWithConditions : DecisionOutcome.Approved;
                if (note is not null) c.DecisionNotes = note;
                break;
            case CaseStatus.Issued:
                c.IssuedUtc = nowUtc;
                break;
        }

        c.Events.Add(new CaseEvent
        {
            AtUtc = nowUtc,
            Actor = actor,
            Type = action,
            FromStatus = from,
            ToStatus = t.To,
            Text = note,
            VisibleToApplicant = action is Submit or PaymentReceived or Validate or RequestInformation or Approve or Refuse or Issue or Withdraw or Expire or Revoke
        });
        return t.To;
    }

    private static string? Guard(PermitCase c, Transition t)
    {
        switch (t.Action)
        {
            case Submit when c.Applicant is null && c.ApplicantId == 0:
                return "The case has no applicant.";
            case Submit when c.Kind != PermitKind.ParkingPermit && string.IsNullOrEmpty(c.Uprn):
                return "A site address must be selected from the address register.";
            case Validate when c.Kind == PermitKind.BuildingPermit && c.SpatialCheckedUtc is null:
                return "Site checks have not been run.";
            case Recommend when c.Consultations.Any(x => x.RespondedUtc is null):
                return "Consultations are still open.";
            case Approve when c.Balance > 0:
                return $"Fees of {c.Balance:0.00} are outstanding.";
            case ConsultationClosed when c.Consultations.Any(x => x.RespondedUtc is null && x.DueDate >= DateOnly.FromDateTime(DateTime.UtcNow)):
                return "Consultations are still within their response period.";
        }
        return null;
    }
}
