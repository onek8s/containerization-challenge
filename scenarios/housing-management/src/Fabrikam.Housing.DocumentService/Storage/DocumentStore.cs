using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fabrikam.Housing.DocumentService.Storage
{
    public class NewDocument
    {
        public DocumentCategory Category { get; set; }
        public string Title { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public int? PropertyId { get; set; }
        public int? TenancyId { get; set; }
        public int? WorkOrderId { get; set; }
        public int? ContractorId { get; set; }
        public bool VisibleToTenant { get; set; }
        public string UploadedBy { get; set; }
        public DateTime? UploadedAt { get; set; }
    }

    /// <summary>
    /// Files live under the store root as yyyy\MM\{key}{ext}; metadata is in housing.Documents.
    /// </summary>
    public class DocumentStore
    {
        private static readonly object IndexGate = new object();
        private readonly StorageOptions _options;
        private readonly IServiceScopeFactory _scopes;
        private readonly IDocumentIndex _index;
        private readonly ITextExtractor _extractor;
        private readonly IThumbnailer _thumbnailer;
        private readonly ILogger<DocumentStore> _logger;

        public DocumentStore(StorageOptions options, IServiceScopeFactory scopes, IDocumentIndex index, ITextExtractor extractor,
            IThumbnailer thumbnailer, ILogger<DocumentStore> logger)
        {
            _options = options;
            _scopes = scopes;
            _index = index;
            _extractor = extractor;
            _thumbnailer = thumbnailer;
            _logger = logger;
            Directory.CreateDirectory(_options.Root);
            Directory.CreateDirectory(_options.ThumbnailFolder);
        }

        public async Task<DocumentRecord> SaveAsync(NewDocument doc, byte[] content)
        {
            var key = Guid.NewGuid();
            var uploadedAt = doc.UploadedAt ?? DateTime.Now;
            var extension = Path.GetExtension(doc.FileName ?? "").ToLowerInvariant();
            var relative = uploadedAt.ToString("yyyy") + "\\" + uploadedAt.ToString("MM") + "\\" + key.ToString("N") + extension;
            var full = FullPath(relative);
            WriteAtomically(full, content);

            string thumbRelative = null;
            try
            {
                var thumb = _thumbnailer.Create(content, doc.ContentType);
                if (thumb != null)
                {
                    thumbRelative = key.ToString("N") + ".jpg";
                    WriteAtomically(Path.Combine(_options.ThumbnailFolder, thumbRelative), thumb);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Thumbnail failed for {File}", doc.FileName);
            }

            var record = new DocumentRecord
            {
                DocumentKey = key,
                Category = doc.Category,
                Title = string.IsNullOrWhiteSpace(doc.Title) ? doc.FileName : doc.Title,
                FileName = Path.GetFileName(doc.FileName ?? "document" + extension),
                ContentType = doc.ContentType ?? "application/octet-stream",
                SizeBytes = content.LongLength,
                Sha256 = Sha256(content),
                StoragePath = relative,
                ThumbnailPath = thumbRelative,
                PropertyId = doc.PropertyId,
                TenancyId = doc.TenancyId,
                WorkOrderId = doc.WorkOrderId,
                ContractorId = doc.ContractorId,
                VisibleToTenant = doc.VisibleToTenant,
                UploadedAt = uploadedAt,
                UploadedBy = doc.UploadedBy ?? "system",
                RetainUntil = RetentionSchedule.RetainUntil(doc.Category, uploadedAt)
            };

            using (var scope = _scopes.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<HousingDbContext>();
                db.Documents.Add(record);
                await db.SaveChangesAsync();
                if (Index(record, content))
                {
                    record.IndexedAt = DateTime.Now;
                    await db.SaveChangesAsync();
                }
                await scope.ServiceProvider.GetRequiredService<AuditWriter>().WriteAsync(record.UploadedBy, "Document", key.ToString(), "Uploaded",
                    new { record.Category, record.FileName, record.TenancyId, record.PropertyId, record.WorkOrderId });
            }
            _logger.LogInformation("Stored document {Key} ({Category}, {Size} bytes) at {Path}", key, record.Category, record.SizeBytes, relative);
            return record;
        }

        public async Task<(DocumentRecord Record, string Path)> FindAsync(Guid key, bool thumbnail = false)
        {
            using (var scope = _scopes.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<HousingDbContext>();
                var record = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.DocumentKey == key && !d.Deleted);
                if (record == null) return (null, null);
                if (thumbnail)
                    return (record, record.ThumbnailPath == null ? null : Path.Combine(_options.ThumbnailFolder, record.ThumbnailPath));
                return (record, FullPath(record.StoragePath));
            }
        }

        public async Task<bool> DeleteAsync(Guid key, string reason, string actor)
        {
            using (var scope = _scopes.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<HousingDbContext>();
                var record = await db.Documents.FirstOrDefaultAsync(d => d.DocumentKey == key && !d.Deleted);
                if (record == null) return false;
                record.Deleted = true;
                await db.SaveChangesAsync();
                TryDelete(FullPath(record.StoragePath));
                if (record.ThumbnailPath != null) TryDelete(Path.Combine(_options.ThumbnailFolder, record.ThumbnailPath));
                lock (IndexGate)
                {
                    _index.Delete(key);
                    _index.Commit();
                }
                await scope.ServiceProvider.GetRequiredService<AuditWriter>().WriteAsync(actor, "Document", key.ToString(), "Deleted", reason);
            }
            return true;
        }

        public IList<IndexHit> Search(string query, int? tenancyId, int? propertyId, int max)
        {
            return _index.Search(query, tenancyId, propertyId, Math.Min(Math.Max(max, 1), 500));
        }

        /// <summary>Indexes documents that were stored while the index was unavailable or before it existed.</summary>
        public async Task<int> ReindexPendingAsync()
        {
            var done = 0;
            using (var scope = _scopes.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<HousingDbContext>();
                var pending = await db.Documents.Where(d => d.IndexedAt == null && !d.Deleted).OrderBy(d => d.Id).Take(5000).ToListAsync();
                foreach (var record in pending)
                {
                    var path = FullPath(record.StoragePath);
                    if (!File.Exists(path))
                    {
                        _logger.LogWarning("Document {Key} is missing from the store ({Path})", record.DocumentKey, path);
                        continue;
                    }
                    if (Index(record, File.ReadAllBytes(path), commit: false))
                    {
                        record.IndexedAt = DateTime.Now;
                        done++;
                    }
                }
                lock (IndexGate) _index.Commit();
                await db.SaveChangesAsync();
            }
            return done;
        }

        public HealthStatus Health()
        {
            var status = new HealthStatus { Machine = Environment.MachineName, Time = DateTime.Now };
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(_options.Root)));
                status.FreeGb = Math.Round(drive.AvailableFreeSpace / 1024.0 / 1024 / 1024, 1);
                status.Store = Directory.Exists(_options.Root);
                using (var scope = _scopes.CreateScope())
                    status.Database = scope.ServiceProvider.GetRequiredService<HousingDbContext>().Database.CanConnect();
            }
            catch (Exception ex)
            {
                status.Error = ex.Message;
            }
            status.Ok = status.Store && status.Database && status.FreeGb > 1;
            return status;
        }

        public class HealthStatus
        {
            public bool Ok { get; set; }
            public bool Store { get; set; }
            public bool Database { get; set; }
            public double FreeGb { get; set; }
            public string Machine { get; set; }
            public DateTime Time { get; set; }
            public string Error { get; set; }
        }

        private bool Index(DocumentRecord record, byte[] content, bool commit = true)
        {
            try
            {
                var text = _extractor.Extract(content, record.ContentType, record.FileName);
                lock (IndexGate)
                {
                    _index.Add(new IndexedDocument
                    {
                        DocumentKey = record.DocumentKey,
                        Title = record.Title,
                        Category = record.Category.ToString(),
                        Text = text,
                        TenancyId = record.TenancyId,
                        PropertyId = record.PropertyId,
                        UploadedAt = record.UploadedAt
                    });
                    if (commit) _index.Commit();
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Indexing failed for {Key}", record.DocumentKey);
                return false;
            }
        }

        private string FullPath(string relative) => Path.Combine(_options.Root, relative);

        private static void WriteAtomically(string path, byte[] content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var tmp = path + ".tmp";
            File.WriteAllBytes(tmp, content);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        private void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Could not delete {Path}", path);
            }
        }

        private static string Sha256(byte[] content)
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(content).Select(b => b.ToString("x2")));
        }
    }
}
