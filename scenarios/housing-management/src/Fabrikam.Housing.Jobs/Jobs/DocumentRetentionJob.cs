using System;
using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Integration.DocumentStore;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fabrikam.Housing.Jobs.Jobs
{
    /// <summary>Deletes documents past their retention date (data retention schedule DP-03).</summary>
    [AutomaticRetry(Attempts = 1)]
    [DisableConcurrentExecution(3600)]
    public class DocumentRetentionJob : IHousingJob
    {
        private readonly HousingDbContext _db;
        private readonly IDocumentServiceClient _documents;
        private readonly AuditWriter _audit;
        private readonly IClock _clock;
        private readonly ILogger<DocumentRetentionJob> _logger;

        public DocumentRetentionJob(HousingDbContext db, IDocumentServiceClient documents, AuditWriter audit, IClock clock, ILogger<DocumentRetentionJob> logger)
        {
            _db = db;
            _documents = documents;
            _audit = audit;
            _clock = clock;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            var today = _clock.Today;
            var expired = await _db.Documents.Where(d => !d.Deleted && d.RetainUntil != null && d.RetainUntil < today)
                .OrderBy(d => d.RetainUntil).Take(500).ToListAsync();
            var deleted = 0;
            foreach (var document in expired)
            {
                try
                {
                    await _documents.DeleteAsync(document.DocumentKey, "Retention schedule DP-03");
                    document.Deleted = true;
                    deleted++;
                    await _audit.WriteAsync("housing-jobs", "Document", document.DocumentKey.ToString(), "Deleted",
                        new { document.Category, document.Title, document.RetainUntil });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete document {Key}", document.DocumentKey);
                }
            }
            await _db.SaveChangesAsync();
            _logger.LogInformation("Document retention: {Deleted} of {Expired} expired documents deleted", deleted, expired.Count);
        }
    }
}
