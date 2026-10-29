namespace StudyData.Core.Documents;

public class SourceDocument
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public int? VisitId { get; set; }
    public string Category { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public string StoredPath { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Length { get; set; }
    public string Sha256 { get; set; } = "";
    public bool Redacted { get; set; }
    public DateTime UploadedAt { get; set; }
    public string UploadedBy { get; set; } = "";
}

public enum JobState
{
    Requested,
    Running,
    Completed,
    Failed
}

/// <summary>Casebook, ODM export or safety report produced by the jobs service.</summary>
public class GeneratedDocument
{
    public int Id { get; set; }
    public int StudyId { get; set; }
    public int? SubjectId { get; set; }

    /// <summary>Casebook, OdmExport, SafetyWorkbook.</summary>
    public string Kind { get; set; } = "";

    public JobState State { get; set; }
    public string RequestedBy { get; set; } = "";
    public DateTime RequestedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Path { get; set; }
    public string? Sha256 { get; set; }
    public string? Error { get; set; }
    public string? Parameters { get; set; }
}

public class Notification
{
    public long Id { get; set; }
    public DateTime At { get; set; }
    public int StudyId { get; set; }
    public int? SiteId { get; set; }
    public string Kind { get; set; } = "";
    public string Text { get; set; } = "";
    public int? SubjectId { get; set; }
    public bool Delivered { get; set; }
}
