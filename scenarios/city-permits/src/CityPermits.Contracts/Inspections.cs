namespace CityPermits.Contracts;

public sealed record InspectionVisitDto(
    int Id,
    int CaseId,
    string CaseReference,
    string Stage,
    string Status,
    DateOnly? ScheduledDate,
    string? Slot,
    int? RouteOrder,
    string? SiteAddress,
    double? Latitude,
    double? Longitude,
    string ApplicantName,
    string? ApplicantPhone,
    string Title,
    IReadOnlyList<ChecklistItemDto> Checklist,
    string? ResultNotes);

public sealed record ChecklistItemDto(string Code, string Text, bool Mandatory, bool? Passed, string? Note);

public sealed record InspectionResultDto(
    string ClientMutationId,
    int InspectionId,
    DateTime StartedUtc,
    DateTime CompletedUtc,
    bool NoAccess,
    string? Notes,
    IReadOnlyList<ChecklistAnswerDto> Answers,
    double? Latitude,
    double? Longitude);

public sealed record ChecklistAnswerDto(string Code, bool? Passed, string? Note);

public sealed record InspectionResultAck(string ClientMutationId, int InspectionId, string Status, bool Duplicate);
