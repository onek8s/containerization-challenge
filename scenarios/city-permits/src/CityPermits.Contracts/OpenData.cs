namespace CityPermits.Contracts;

public sealed record IssuedPermitDto(
    string Reference,
    string Kind,
    string Title,
    string? Ward,
    string? SiteAddress,
    double? Latitude,
    double? Longitude,
    DateTime? DecisionDate,
    string? Outcome,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    string? WorkType,
    string? BusinessCategory,
    string? TradingName);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public sealed record StatisticsRow(string Kind, int Year, int Month, int Received, int Decided, int Approved, int Refused, double MedianDaysToDecision);
