namespace StudyData.Documents.Wpf;

// Bound from the XAML templates; must stay public with plain properties.

public sealed class CoverPageViewModel
{
    public string StudyCode { get; init; } = "";
    public string StudyTitle { get; init; } = "";
    public string Sponsor { get; init; } = "";
    public string SubjectNumber { get; init; } = "";
    public string Site { get; init; } = "";
    public string Investigator { get; init; } = "";
    public string Status { get; init; } = "";
    public string RandomisationNumber { get; init; } = "";
    public string FormCount { get; init; } = "";
    public string Generated { get; init; } = "";
}

public sealed class FormPageViewModel
{
    public string StudyCode { get; init; } = "";
    public string Sponsor { get; init; } = "";
    public string SubjectLine { get; init; } = "";
    public string SiteLine { get; init; } = "";
    public string FormTitle { get; init; } = "";
    public string VisitTitle { get; init; } = "";
    public string Status { get; init; } = "";
    public string SignatureLine { get; init; } = "";
    public string Footer { get; init; } = "";
    public string PageLabel { get; init; } = "";
    public IReadOnlyList<FieldRowViewModel> Rows { get; init; } = Array.Empty<FieldRowViewModel>();
}

public sealed class FieldRowViewModel
{
    public string Code { get; init; } = "";
    public string Label { get; init; } = "";
    public string ValueText { get; init; } = "";
    public string Marks { get; init; } = "";
    public string Shade { get; init; } = "White";
}

public sealed class AuditPageViewModel
{
    public string Title { get; init; } = "";
    public string SubjectLine { get; init; } = "";
    public string PageLabel { get; init; } = "";
    public IReadOnlyList<AuditRowViewModel> Rows { get; init; } = Array.Empty<AuditRowViewModel>();
}

public sealed class AuditRowViewModel
{
    public string At { get; init; } = "";
    public string User { get; init; } = "";
    public string Text { get; init; } = "";
}
