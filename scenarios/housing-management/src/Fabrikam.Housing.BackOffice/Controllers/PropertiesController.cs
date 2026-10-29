using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Fabrikam.Housing.Assets;
using Fabrikam.Housing.BackOffice.Infrastructure;
using Fabrikam.Housing.BackOffice.Models;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Compliance;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Data.Queries;
using Fabrikam.Housing.Integration.DocumentStore;
using Fabrikam.Housing.Integration.Geo;
using Fabrikam.Housing.Tenancies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fabrikam.Housing.BackOffice.Controllers
{
    public class PropertiesController : Controller
    {
        private const int PageSize = 25;
        private static readonly ComplianceType[] PropertyTypes =
            { ComplianceType.GasSafety, ComplianceType.Electrical, ComplianceType.SmokeAndCoAlarms, ComplianceType.Asbestos };

        private readonly HousingDbContext _db;
        private readonly RentAccountQueries _rent;
        private readonly IDocumentServiceClient _documents;
        private readonly IPostcodeLookup _postcodes;
        private readonly IPatchLocator _patches;
        private readonly AuditWriter _audit;
        private readonly IClock _clock;
        private readonly IMapper _mapper;
        private readonly ILogger<PropertiesController> _logger;

        public PropertiesController(HousingDbContext db, RentAccountQueries rent, IDocumentServiceClient documents, IPostcodeLookup postcodes,
            IPatchLocator patches, AuditWriter audit, IClock clock, IMapper mapper, ILogger<PropertiesController> logger)
        {
            _db = db;
            _rent = rent;
            _documents = documents;
            _postcodes = postcodes;
            _patches = patches;
            _audit = audit;
            _clock = clock;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IActionResult> Index(PropertySearchViewModel model)
        {
            if (model.Patch == null && string.IsNullOrEmpty(model.Q) && string.IsNullOrEmpty(model.Estate))
                model.Patch = User.Patch();

            var query = _db.Properties.AsNoTracking().Include(p => p.Estate).AsQueryable();
            if (!string.IsNullOrWhiteSpace(model.Q))
            {
                var q = model.Q.Trim();
                if (Postcode.TryNormalise(q, out var pc))
                    query = query.Where(p => p.Postcode == pc);
                else
                    query = query.Where(p => p.Reference == q.ToUpper() || p.AddressLine1.Contains(q) || p.Postcode.StartsWith(q.ToUpper()));
            }
            if (!string.IsNullOrEmpty(model.Estate)) query = query.Where(p => p.Estate.Code == model.Estate);
            if (!string.IsNullOrEmpty(model.Patch)) query = query.Where(p => p.PatchCode == model.Patch);
            if (model.Status.HasValue) query = query.Where(p => p.Status == model.Status.Value);

            model.Total = await query.CountAsync();
            model.Page = Math.Max(1, model.Page);
            model.Results = await query.OrderBy(p => p.Estate.Code).ThenBy(p => p.Reference)
                .Skip((model.Page - 1) * PageSize).Take(PageSize)
                .ProjectTo<PropertyListItem>(_mapper.ConfigurationProvider)
                .ToListAsync();
            model.Estates = await _db.Estates.AsNoTracking().OrderBy(e => e.Name).ToListAsync();
            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var property = await _db.Properties.AsNoTracking().Include(p => p.Estate).SingleOrDefaultAsync(p => p.Id == id);
            if (property == null) return NotFound();

            var tenancy = await _db.Tenancies.AsNoTracking().Include(t => t.Tenants)
                .Where(t => t.PropertyId == id && t.Status != TenancyStatus.Ended)
                .OrderByDescending(t => t.StartDate).FirstOrDefaultAsync();
            var certificates = await _db.Certificates.AsNoTracking().Where(c => c.PropertyId == id).ToListAsync();
            var today = _clock.Today;
            var now = _clock.Now;

            var model = new PropertyDetailViewModel
            {
                Property = property,
                Tenancy = tenancy,
                Balance = tenancy == null ? (decimal?)null : await _rent.BalanceAsync(tenancy.Id),
                Compliance = PropertyTypes.Select(type =>
                {
                    var latest = ComplianceRules.Latest(certificates, type);
                    return new ComplianceLine { Type = type, Latest = latest, Status = ComplianceRules.StatusOf(latest, ComplianceRules.AppliesTo(type, property), today) };
                }).ToList(),
                Repairs = (await _db.WorkOrders.AsNoTracking().Include(w => w.Property).Include(w => w.Contractor)
                    .Where(w => w.PropertyId == id).OrderByDescending(w => w.ReportedAt).Take(50).ToListAsync())
                    .Select(w =>
                    {
                        var item = _mapper.Map<WorkOrderListItem>(w);
                        item.Overdue = w.IsOpen && w.TargetDate < now;
                        return item;
                    }).ToList()
            };

            try
            {
                model.Documents = await _documents.SearchAsync(null, propertyId: id, max: 30);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Document service search failed for property {PropertyId}", id);
                model.Documents = new List<SearchHit>();
                model.DocumentError = "The document service is not available at the moment.";
            }
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Geocode(int id)
        {
            var property = await _db.Properties.FindAsync(id);
            if (property == null) return NotFound();

            var result = await _postcodes.LookupAsync(property.Postcode);
            if (result == null)
            {
                TempData["Error"] = $"Postcode {property.Postcode} was not found.";
                return RedirectToAction(nameof(Details), new { id });
            }

            property.Latitude = result.Latitude;
            property.Longitude = result.Longitude;
            var patch = await _patches.FindPatchAsync(result.Latitude, result.Longitude);
            var message = $"Location set to {result.Latitude:0.0000}, {result.Longitude:0.0000} ({result.Ward}).";
            if (!string.IsNullOrEmpty(patch) && patch != property.PatchCode)
            {
                message += $" Patch changed from {property.PatchCode} to {patch}.";
                property.PatchCode = patch;
            }
            property.ModifiedAt = _clock.Now;
            property.ModifiedBy = User.Login();
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(User.Login(), "Property", property.Reference, "Geocoded", new { result.Latitude, result.Longitude, patch });
            TempData["Message"] = message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
