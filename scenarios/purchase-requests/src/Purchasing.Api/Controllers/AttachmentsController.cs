using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;
using Contoso.Purchasing.Api.Infrastructure;
using Contoso.Purchasing.Core;
using Contoso.Purchasing.Data;

namespace Contoso.Purchasing.Api.Controllers
{
    public class AttachmentsController : PurchasingController
    {
        private static readonly string[] AllowedExtensions = { ".pdf", ".docx", ".xlsx", ".png", ".jpg", ".msg", ".txt" };

        [HttpPost, Route("api/requests/{requestId:int}/attachments")]
        public async Task<AttachmentInfo> Upload(int requestId)
        {
            if (!Request.Content.IsMimeMultipartContent())
            {
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
            }

            var repository = new RequestRepository(Db);
            var request = repository.Get(requestId);
            if (request == null) throw new HttpResponseException(HttpStatusCode.NotFound);
            if (!CanView(request)) throw new HttpResponseException(HttpStatusCode.Forbidden);

            var provider = await Request.Content.ReadAsMultipartAsync(new MultipartMemoryStreamProvider());
            var part = provider.Contents.FirstOrDefault(c => c.Headers.ContentDisposition != null && !string.IsNullOrEmpty(c.Headers.ContentDisposition.FileName));
            if (part == null) throw new PolicyException("No file was uploaded.");

            var fileName = Path.GetFileName(part.Headers.ContentDisposition.FileName.Trim('"'));
            if (!AllowedExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant()))
            {
                throw new PolicyException("Files of type " + Path.GetExtension(fileName) + " are not accepted.");
            }

            var bytes = await part.ReadAsByteArrayAsync();
            if (bytes.Length == 0 || bytes.Length > AppSettings.MaxAttachmentBytes)
            {
                throw new PolicyException("Attachments must be between 1 byte and " + (AppSettings.MaxAttachmentBytes / 1024) + " KB.");
            }

            var store = new AttachmentStore(AppSettings.AttachmentRoot);
            string storedName;
            using (var stream = new MemoryStream(bytes))
            {
                storedName = store.Save(stream, fileName, Now);
            }

            return repository.AddAttachment(new AttachmentInfo
            {
                RequestId = requestId,
                FileName = fileName,
                ContentType = part.Headers.ContentType != null ? part.Headers.ContentType.MediaType : "application/octet-stream",
                SizeBytes = bytes.Length,
                UploadedBy = CurrentUser.Account,
                UploadedAt = Now
            }, storedName);
        }

        [HttpGet, Route("api/attachments/{id:int}")]
        public HttpResponseMessage Download(int id)
        {
            var repository = new RequestRepository(Db);
            string storedName;
            var attachment = repository.GetAttachment(id, out storedName);
            if (attachment == null) throw new HttpResponseException(HttpStatusCode.NotFound);

            var request = repository.Get(attachment.RequestId);
            if (!CanView(request)) throw new HttpResponseException(HttpStatusCode.Forbidden);

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new AttachmentStore(AppSettings.AttachmentRoot).Open(storedName))
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(attachment.ContentType);
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = attachment.FileName };
            return response;
        }
    }
}
