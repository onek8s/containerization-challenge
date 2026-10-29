namespace Litware.DocControl.Packaging;

/// <summary>One document revision to include in a transmittal package.</summary>
public sealed record PackageEntry(string DocumentNumber, string Revision, string Title, string FileName, Func<Stream> Open);

public sealed record PackagedFile(string DocumentNumber, string Revision, string Title, string EntryName, long Size, string Sha256);

public sealed record TransmittalHeader(
    string Number,
    string ProjectCode,
    string ProjectName,
    string Purpose,
    string RecipientCompany,
    string RecipientName,
    string IssuedBy,
    DateTime IssuedUtc,
    string? Notes);
