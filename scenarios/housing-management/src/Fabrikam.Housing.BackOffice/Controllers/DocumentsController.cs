using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.BackOffice.Infrastructure;
using Fabrikam.Housing.BackOffice.Models;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Integration.DocumentStore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Fabrikam.Housing.BackOffice.Controllers
{
    public class DocumentsController : Controller
    {
        private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".docx", ".doc", ".xlsx", ".txt", ".msg", ".eml" };

        private readonly IDocumentServiceClient _documents;
        private readonly AuditWriter _audit;
        private readonly ILogger<DocumentsController> _logger;

        public DocumentsController(IDocumentServiceClient documents, AuditWriter audit, ILogger<DocumentsController> logger)
        {
            _documents = documents;
            _audit = audit;
            _logger = logger;
        }

        public async Task<IActionResult> Index(DocumentSearchViewModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.Q) || model.TenancyId.HasValue || model.PropertyId.HasValue)
            {
                try
                {
                    model.Hits = await _documents.SearchAsync(model.Q, model.TenancyId, model.PropertyId, 100);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Document search failed for '{Query}'", model.Q);
                    model.Error = "The document service did not respond. Try again in a few minutes.";
                }
            }
            return View(model);
        }

        public async Task<IActionResult> Download(Guid id)
        {
            var content = await _documents.DownloadAsync(id);
            if (content == null) return NotFound();
            await _audit.WriteAsync(User.Login(), "Document", id.ToString(), "Downloaded");
            return File(content.Content, content.ContentType ?? "application/octet-stream", content.FileName);
        }

        public async Task<IActionResult> Thumbnail(Guid id)
        {
            var content = await _documents.ThumbnailAsync(id);
            if (content == null) return NotFound();
            Response.Headers["Cache-Control"] = "private, max-age=3600";
            return File(content.Content, content.ContentType ?? "image/jpeg");
        }

        public IActionResult Upload(int? propertyId, int? tenancyId)
        {
            return View(new DocumentUploadInput { PropertyId = propertyId, TenancyId = tenancyId });
        }

        [HttpPost]
        public async Task<IActionResult> Upload(DocumentUploadInput input, IFormFile file)
        {
            if (file == null || file.Length == 0) ModelState.AddModelError("file", "Choose a file.");
            else if (!AllowedExtensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant())) ModelState.AddModelError("file", "This file type is not allowed.");
            if (!input.PropertyId.HasValue && !input.TenancyId.HasValue) ModelState.AddModelError("", "A document must belong to a property or a tenancy.");
            if (!ModelState.IsValid) return View(input);

            Guid key;
            using (var stream = file.OpenReadStream())
            {
                var record = await _documents.UploadAsync(new DocumentUpload
                {
                    Category = input.Category,
                    Title = input.Title,
                    FileName = Path.GetFileName(file.FileName),
                    ContentType = string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType,
                    PropertyId = input.PropertyId,
                    TenancyId = input.TenancyId,
                    VisibleToTenant = input.VisibleToTenant,
                    UploadedBy = User.Login()
                }, stream);
                key = record?.DocumentKey ?? Guid.Empty;
            }
            await _audit.WriteAsync(User.Login(), "Document", key.ToString(), "Uploaded", new { input.Title, input.Category });
            TempData["Message"] = "Document uploaded.";
            if (input.TenancyId.HasValue) return RedirectToAction("Details", "Tenancies", new { id = input.TenancyId });
            return RedirectToAction("Details", "Properties", new { id = input.PropertyId });
        }
    }
}
