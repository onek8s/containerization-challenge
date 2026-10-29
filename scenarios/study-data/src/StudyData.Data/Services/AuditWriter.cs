using StudyData.Core.Services;
using StudyData.Core.Subjects;

namespace StudyData.Data.Services;

/// <summary>Audit trail rows are added in the same SaveChanges as the change itself.</summary>
public class AuditWriter
{
    private readonly StudyDataContext _db;
    private readonly IClock _clock;

    public AuditWriter(StudyDataContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public void Add(string user, string entityType, string entityKey, int? subjectId, string action,
        string? oldValue = null, string? newValue = null, string? reason = null)
    {
        _db.Audit.Add(new AuditEntry
        {
            At = _clock.UtcNow,
            User = user,
            EntityType = entityType,
            EntityKey = entityKey,
            SubjectId = subjectId,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            Reason = reason
        });
    }
}
