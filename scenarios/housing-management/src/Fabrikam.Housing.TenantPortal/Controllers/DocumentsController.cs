using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Integration.DocumentStore;
using Fabrikam.Housing.TenantPortal.Models;
using Fabrikam.Housing.TenantPortal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fabrikam.Housing.TenantPortal.Controllers
{
    public class DocumentsController : Controller
    {
        private readonly CurrentTenant _current;
        private readonly HousingDbContext _db;
        private readonly IDocumentServiceClient _documents;
        private readonly IMapper _mapper;
        private readonly ILogger<DocumentsController> _logger;

        public DocumentsController(CurrentTenant current, HousingDbContext db, IDocumentServiceClient documents, IMapper mapper, ILogger<DocumentsController> logger)
        {
            _current = current;
            _db = db;
            _documents = documents;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var tenancy = await _current.TenancyAsync();
            var records = await _db.Documents.AsNoTracking()
                .Where(d => d.TenancyId == tenancy.Id && d.VisibleToTenant && !d.Deleted)
                .OrderByDescending(d => d.UploadedAt).ToListAsync();
            return View(_mapper.Map<List<DocumentViewModel>>(records));
        }

        public async Task<IActionResult> Download(Guid id)
        {
            var tenancy = await _current.TenancyAsync();
            var record = await _db.Documents.AsNoTracking()
                .FirstOrDefaultAsync(d => d.DocumentKey == id && d.TenancyId == tenancy.Id && d.VisibleToTenant && !d.Deleted);
            if (record == null) return NotFound();
            try
            {
                var content = await _documents.DownloadAsync(id);
                return File(content.Content, content.ContentType ?? record.ContentType, content.FileName ?? record.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Document {Key} could not be fetched from the document service", id);
                TempData["Error"] = "This document is not available at the moment. Please try again later.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
