using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.Documents;
using Fabrikam.Housing.Integration.DocumentStore;

namespace Fabrikam.Housing.Api.Tests.BackOffice
{
    public class FakeDocumentServiceClient : IDocumentServiceClient
    {
        public ConcurrentDictionary<Guid, (DocumentUpload Upload, byte[] Content)> Stored { get; } = new ConcurrentDictionary<Guid, (DocumentUpload, byte[])>();

        public async Task<DocumentRecord> UploadAsync(DocumentUpload upload, Stream content)
        {
            using (var ms = new MemoryStream())
            {
                await content.CopyToAsync(ms);
                var key = Guid.NewGuid();
                Stored[key] = (upload, ms.ToArray());
                return new DocumentRecord { DocumentKey = key, Title = upload.Title, FileName = upload.FileName, Category = upload.Category, SizeBytes = ms.Length, UploadedAt = DateTime.Now };
            }
        }

        public Task<DocumentContent> DownloadAsync(Guid documentKey)
        {
            return Task.FromResult(Stored.TryGetValue(documentKey, out var d)
                ? new DocumentContent { FileName = d.Upload.FileName, ContentType = d.Upload.ContentType, Content = d.Content }
                : null);
        }

        public Task<DocumentContent> ThumbnailAsync(Guid documentKey) => Task.FromResult<DocumentContent>(null);

        public Task<IList<SearchHit>> SearchAsync(string query, int? tenancyId = null, int? propertyId = null, int max = 50)
        {
            IList<SearchHit> hits = Stored
                .Where(kv => (tenancyId == null || kv.Value.Upload.TenancyId == tenancyId) && (propertyId == null || kv.Value.Upload.PropertyId == propertyId))
                .Where(kv => string.IsNullOrEmpty(query) || kv.Value.Upload.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(max)
                .Select(kv => new SearchHit { DocumentKey = kv.Key, Title = kv.Value.Upload.Title, Category = kv.Value.Upload.Category.ToString(), PropertyId = kv.Value.Upload.PropertyId, TenancyId = kv.Value.Upload.TenancyId, UploadedAt = DateTime.Now })
                .ToList();
            return Task.FromResult(hits);
        }

        public Task DeleteAsync(Guid documentKey, string reason)
        {
            Stored.TryRemove(documentKey, out _);
            return Task.CompletedTask;
        }
    }
}
