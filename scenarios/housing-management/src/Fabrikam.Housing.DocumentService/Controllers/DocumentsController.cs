using System;
using System.IO;
using System.Threading.Tasks;
using Fabrikam.Housing.DocumentService.Storage;
using Fabrikam.Housing.Documents;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Fabrikam.Housing.DocumentService.Controllers
{
    [ApiController]
    [Route("api/documents")]
    public class DocumentsController : ControllerBase
    {
        private readonly DocumentStore _store;

        public DocumentsController(DocumentStore store)
        {
            _store = store;
        }

        public class UploadForm
        {
            public DocumentCategory Category { get; set; }
            public string Title { get; set; }
            public int? PropertyId { get; set; }
            public int? TenancyId { get; set; }
            public int? WorkOrderId { get; set; }
            public int? ContractorId { get; set; }
            public bool VisibleToTenant { get; set; }
            public string UploadedBy { get; set; }
            public IFormFile File { get; set; }
        }

        [HttpPost]
        public async Task<ActionResult<DocumentRecord>> Upload([FromForm] UploadForm form)
        {
            if (form.File == null || form.File.Length == 0) return BadRequest("No file uploaded.");
            byte[] content;
            using (var ms = new MemoryStream())
            {
                await form.File.CopyToAsync(ms);
                content = ms.ToArray();
            }
            var record = await _store.SaveAsync(new NewDocument
            {
                Category = form.Category,
                Title = form.Title,
                FileName = form.File.FileName,
                ContentType = form.File.ContentType,
                PropertyId = form.PropertyId,
                TenancyId = form.TenancyId,
                WorkOrderId = form.WorkOrderId,
                ContractorId = form.ContractorId,
                VisibleToTenant = form.VisibleToTenant,
                UploadedBy = form.UploadedBy
            }, content);
            return Ok(record);
        }

        [HttpGet("{key:guid}/content")]
        public async Task<IActionResult> Content(Guid key)
        {
            var (record, path) = await _store.FindAsync(key);
            if (record == null || !System.IO.File.Exists(path)) return NotFound();
            return PhysicalFile(path, record.ContentType, record.FileName);
        }

        [HttpGet("{key:guid}/thumbnail")]
        public async Task<IActionResult> Thumbnail(Guid key)
        {
            var (record, path) = await _store.FindAsync(key, thumbnail: true);
            if (record == null || path == null || !System.IO.File.Exists(path)) return NotFound();
            return PhysicalFile(path, "image/jpeg", Path.GetFileNameWithoutExtension(record.FileName) + "-thumb.jpg");
        }

        [HttpGet("search")]
        public IActionResult Search(string q, int? tenancyId, int? propertyId, int max = 50)
        {
            return Ok(_store.Search(q, tenancyId, propertyId, max));
        }

        [HttpDelete("{key:guid}")]
        public async Task<IActionResult> Delete(Guid key, string reason, string actor = null)
        {
            return await _store.DeleteAsync(key, reason, actor ?? "api") ? (IActionResult)NoContent() : NotFound();
        }
    }
}
