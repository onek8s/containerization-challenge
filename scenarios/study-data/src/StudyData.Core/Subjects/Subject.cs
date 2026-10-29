using StudyData.Core.Design;

namespace StudyData.Core.Subjects;

public enum SubjectStatus
{
    Screening,
    ScreenFailed,
    Randomised,
    OnTreatment,
    FollowUp,
    Completed,
    Withdrawn
}

public class Subject
{
    public int Id { get; set; }
    public int StudyId { get; set; }
    public Study? Study { get; set; }
    public int SiteId { get; set; }
    public Site? Site { get; set; }

    /// <summary>Subject number, for example 101-004.</summary>
    public string Number { get; set; } = "";

    public string? Initials { get; set; }
    public int BirthYear { get; set; }
    public string Sex { get; set; } = "";
    public DateOnly? ConsentDate { get; set; }
    public SubjectStatus Status { get; set; }
    public int? ArmId { get; set; }
    public Arm? Arm { get; set; }
    public DateTime? RandomisedAt { get; set; }
    public string? RandomisationNumber { get; set; }
    public string? WithdrawalReason { get; set; }

    public List<Visit> Visits { get; set; } = new();
}

public enum VisitStatus
{
    Planned,
    Started,
    Completed,
    Missed
}

public class Visit
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public int VisitDefinitionId { get; set; }
    public VisitDefinition? Definition { get; set; }

    /// <summary>Repeat number for unscheduled visits.</summary>
    public int Repeat { get; set; } = 1;

    public DateOnly? Date { get; set; }
    public VisitStatus Status { get; set; }
    public bool OutOfWindow { get; set; }

    public List<FormInstance> Forms { get; set; } = new();
}

public enum FormStatus
{
    NotStarted,
    InProgress,
    Complete,
    Verified,
    Signed,
    Locked
}

public class FormInstance
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public Visit? Visit { get; set; }
    public int FormDefinitionId { get; set; }
    public FormDefinition? Definition { get; set; }

    /// <summary>Row number for repeating forms.</summary>
    public int Row { get; set; } = 1;

    public FormStatus Status { get; set; }
    public DateTime? LastSavedAt { get; set; }
    public string? LastSavedBy { get; set; }
    public DateTime? SignedAt { get; set; }
    public string? SignedBy { get; set; }

    /// <summary>Workbook version used for the last derivation.</summary>
    public string? DerivedWith { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public List<FieldValue> Values { get; set; } = new();
}

public class FieldValue
{
    public int Id { get; set; }
    public int FormInstanceId { get; set; }
    public FormInstance? Form { get; set; }
    public string FieldCode { get; set; } = "";

    /// <summary>Value in invariant culture (dates as yyyy-MM-dd).</summary>
    public string? Value { get; set; }

    public bool Verified { get; set; }
    public string? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }
}

public class AuditEntry
{
    public long Id { get; set; }
    public DateTime At { get; set; }
    public string User { get; set; } = "";

    /// <summary>Subject, Form, Field, Query, Sdv, Signature, Coding, Design ...</summary>
    public string EntityType { get; set; } = "";

    public string EntityKey { get; set; } = "";
    public int? SubjectId { get; set; }
    public string Action { get; set; } = "";
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Reason { get; set; }
}
