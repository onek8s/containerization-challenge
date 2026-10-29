using Humongous.Quoting.Domain;

namespace Humongous.Quoting.Contracts.Hubs;

/// <summary>Methods the quote hub (<c>/hubs/quotes</c>) calls on connected clients.</summary>
public interface IQuoteHubClient
{
    Task QuoteChanged(QuoteNotice notice);

    Task PolicyChanged(PolicyNotice notice);

    Task DocumentReady(DocumentNotice notice);
}

public sealed record QuoteNotice(Guid QuoteId, string Reference, string AgencyCode, QuoteStatus Status, decimal? Total, string? Message);

public sealed record PolicyNotice(Guid PolicyId, string PolicyNumber, string AgencyCode, string Event, string? Message);

public sealed record DocumentNotice(Guid DocumentId, string DocumentType, Guid? QuoteId, Guid? PolicyId, string FileName);

public static class HubGroups
{
    public const string Underwriters = "underwriters";

    public static string Agency(string code) => $"agency:{code}";

    public static string Quote(Guid id) => $"quote:{id:N}";
}
