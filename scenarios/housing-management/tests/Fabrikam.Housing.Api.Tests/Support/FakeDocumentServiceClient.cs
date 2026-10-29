using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Fabrikam.Housing.Documents;
using Fabrikam.Housing.Integration.DocumentStore;

namespace Fabrikam.Housing.Api.Tests.Support
{
    public class FakeDocumentServiceClient : IDocumentServiceClient
    {

        public ConcurrentBag<(DocumentUpload Upload, byte[] Content)> Uploads { get; } = new ConcurrentBag<(DocumentUpload, byte[])>();
        public ConcurrentBag<Guid> Deleted { get; } = new ConcurrentBag<Guid>();

        public async Task<DocumentRecord> UploadAsync(DocumentUpload upload, Stream content)
        {
            var ms = new MemoryStream();
            await content.CopyToAsync(ms);
            Uploads.Add((upload, ms.ToArray()));
            return new DocumentRecord
            {
                // Not stored in the database by the fake; 0 means "no row".
                Id = 0,
                DocumentKey = Guid.NewGuid(),
                FileName = upload.FileName,
                ContentType = upload.ContentType,
                Category = upload.Category,
                Title = upload.Title,
                SizeBytes = ms.Length
            };
        }

        public Task<DocumentContent> DownloadAsync(Guid documentKey) => Task.FromResult<DocumentContent>(null);

        public Task<DocumentContent> ThumbnailAsync(Guid documentKey) => Task.FromResult<DocumentContent>(null);

        public Task<IList<SearchHit>> SearchAsync(string query, int? tenancyId = null, int? propertyId = null, int max = 50) =>
            Task.FromResult<IList<SearchHit>>(new List<SearchHit>());

        public Task DeleteAsync(Guid documentKey, string reason)
        {
            Deleted.Add(documentKey);
            return Task.CompletedTask;
        }
    }
}
