using System.Text.Json;

namespace CityPermits.Data;

public static class OutboxTypes
{
    public const string ApplicantNotification = "applicant-notification";
    public const string GenerateDecisionDocuments = "generate-decision-documents";
    public const string FileToRecords = "file-to-records";
    public const string OfficerNotification = "officer-notification";
    public const string RunSiteChecks = "run-site-checks";
}

public sealed class OutboxWriter
{
    private readonly PermitsDbContext _db;

    public OutboxWriter(PermitsDbContext db) => _db = db;

    /// <summary>Adds a message to the outbox; it is saved with the caller's SaveChanges.</summary>
    public void Add(string type, object payload) => _db.Outbox.Add(new OutboxMessage
    {
        Type = type,
        Payload = JsonSerializer.Serialize(payload),
        CreatedUtc = DateTime.UtcNow
    });
}
