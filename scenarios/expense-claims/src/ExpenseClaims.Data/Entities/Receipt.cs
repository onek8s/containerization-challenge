namespace ExpenseClaims.Data.Entities;

/// <summary>Uploaded receipt. The file itself is kept in the receipt store, <see cref="StoragePath"/> is relative to its root.</summary>
public class Receipt
{
    public int Id { get; set; }
    public Guid FileId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Length { get; set; }
    public string Sha256 { get; set; } = "";
    public string StoragePath { get; set; } = "";
    public int UploadedById { get; set; }
    public DateTime UploadedAt { get; set; }
}
