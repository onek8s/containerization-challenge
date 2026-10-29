namespace Litware.DocControl.Indexer;

public sealed class IndexerOptions
{
    public const string SectionName = "DocControl:Indexer";

    public int ScanSeconds { get; set; } = 60;

    /// <summary>Files changed more recently than this are left alone; the copy may still be running.</summary>
    public int MinFileAgeSeconds { get; set; } = 30;

    public string ProcessedFolder { get; set; } = "_processed";
    public string RejectedFolder { get; set; } = "_rejected";

    /// <summary>Reject PDFs whose title (from the title block) does not contain the document number.</summary>
    public bool CheckPdfTitle { get; set; } = true;
}
