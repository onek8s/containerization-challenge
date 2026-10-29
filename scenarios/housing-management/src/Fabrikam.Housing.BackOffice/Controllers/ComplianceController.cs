using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.BackOffice.Infrastructure;
using Fabrikam.Housing.BackOffice.Models;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Compliance;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Documents;
using Fabrikam.Housing.Integration.DocumentStore;
using Fabrikam.Housing.Repairs;
using Fabrikam.Housing.Tenancies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fabrikam.Housing.BackOffice.Controllers
{
    public class ComplianceController : Controller
    {
        private static readonly ComplianceType[] PropertyLevelTypes = { ComplianceType.GasSafety, ComplianceType.Electrical, ComplianceType.Asbestos };

        private readonly HousingDbContext _db;
        private readonly IDocumentServiceClient _documents;
        private readonly RepairRules _rules;
        private readonly CounterService _counters;
        private readonly AuditWriter _audit;
        private readonly IClock _clock;

        public ComplianceController(HousingDbContext db, IDocumentServiceClient documents, RepairRules rules, CounterService counters, AuditWriter audit, IClock clock)
        {
            _db = db;
            _documents = documents;
            _rules = rules;
            _counters = counters;
            _audit = audit;
            _clock = clock;
        }

        public async Task<IActionResult> Index(ComplianceType? type, int daysAhead = ComplianceRules.DueSoonDays, string patch = null)
        {
            var today = _clock.Today;
            var properties = await _db.Properties.AsNoTracking().Where(p => p.Status != Assets.PropertyStatus.Disposed).ToListAsync();
            var certificates = await _db.Certificates.AsNoTracking().Where(c => !c.Superseded && c.PropertyId != null).ToListAsync();
            var byProperty = certificates.ToLookup(c => c.PropertyId.Value);

            var counts = PropertyLevelTypes.ToDictionary(t => t, t => (IDictionary<ComplianceStatus, int>)new Dictionary<ComplianceStatus, int>());
            var due = new List<ComplianceDueItem>();
            foreach (var p in properties)
            {
                foreach (var t in PropertyLevelTypes)
                {
                    var latest = ComplianceRules.Latest(byProperty[p.Id], t);
                    var status = ComplianceRules.StatusOf(latest, ComplianceRules.AppliesTo(t, p), today);
                    counts[t].TryGetValue(status, out var n);
                    counts[t][status] = n + 1;

                    if (status == ComplianceStatus.NotApplicable || (type.HasValue && type.Value != t)) continue;
                    if (!string.IsNullOrEmpty(patch) && p.PatchCode != patch) continue;
                    var expires = latest?.ExpiresOn;
                    if (expires == null || expires.Value <= today.AddDays(daysAhead))
                        due.Add(new ComplianceDueItem { PropertyId = p.Id, PropertyReference = p.Reference, Address = p.AddressLine1, PatchCode = p.PatchCode, Type = t, ExpiresOn = expires, Status = status });
                }
            }

            ViewBag.Patch = patch;
            ViewBag.Communal = await _db.Certificates.AsNoTracking().Where(c => !c.Superseded && c.EstateId != null).OrderBy(c => c.ExpiresOn).ToListAsync();
            ViewBag.Estates = await _db.Estates.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.Name);
            return View(new ComplianceDashboardViewModel
            {
                Counts = counts,
                Due = due.OrderBy(d => d.ExpiresOn ?? DateTime.MinValue).ToList(),
                DaysAhead = daysAhead,
                Type = type
            });
        }

        [Authorize(Policies.Compliance)]
        public async Task<IActionResult> Upload(int propertyId, ComplianceType type = ComplianceType.GasSafety)
        {
            var property = await _db.Properties.FindAsync(propertyId);
            if (property == null) return NotFound();
            return View(new CertificateUploadInput
            {
                PropertyId = propertyId,
                PropertyAddress = property.SingleLineAddress,
                Type = type,
                Contractors = await _db.Contractors.AsNoTracking().Where(c => c.Active).OrderBy(c => c.Name).ToListAsync()
            });
        }

        [HttpPost, Authorize(Policies.Compliance)]
        public async Task<IActionResult> Upload(CertificateUploadInput input, IFormFile file)
        {
            var property = await _db.Properties.FindAsync(input.PropertyId);
            if (property == null) return NotFound();

            if (input.Type == ComplianceType.GasSafety)
            {
                try { ComplianceRules.ValidateGasSafeNumber(input.EngineerRegistration); }
                catch (BusinessRuleException ex) { ModelState.AddModelError(nameof(input.EngineerRegistration), ex.Message); }
            }
            if (input.InspectedOn.Date > _clock.Today) ModelState.AddModelError(nameof(input.InspectedOn), "The inspection date cannot be in the future.");
            if (file == null || file.Length == 0) ModelState.AddModelError("file", "Attach the certificate (PDF).");
            else if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)) ModelState.AddModelError("file", "Certificates must be PDF files.");
            if (!ModelState.IsValid)
            {
                input.PropertyAddress = property.SingleLineAddress;
                input.Contractors = await _db.Contractors.AsNoTracking().Where(c => c.Active).OrderBy(c => c.Name).ToListAsync();
                return View(input);
            }

            DocumentRecord document;
            using (var stream = file.OpenReadStream())
            {
                document = await _documents.UploadAsync(new DocumentUpload
                {
                    Category = DocumentCategory.Certificate,
                    Title = $"{input.Type} certificate {input.CertificateNumber}",
                    FileName = Path.GetFileName(file.FileName),
                    ContentType = "application/pdf",
                    PropertyId = property.Id,
                    ContractorId = input.ContractorId,
                    VisibleToTenant = input.Type == ComplianceType.GasSafety || input.Type == ComplianceType.Electrical,
                    UploadedBy = User.Login()
                }, stream);
            }

            var now = _clock.Now;
            foreach (var previous in _db.Certificates.Where(c => c.PropertyId == property.Id && c.Type == input.Type && !c.Superseded))
                previous.Superseded = true;

            var certificate = new ComplianceCertificate
            {
                PropertyId = property.Id,
                Type = input.Type,
                CertificateNumber = input.CertificateNumber.Trim(),
                InspectedOn = input.InspectedOn.Date,
                ExpiresOn = ComplianceRules.ExpiryFor(input.Type, input.InspectedOn.Date),
                Outcome = input.Outcome,
                EngineerName = input.EngineerName,
                EngineerRegistration = input.EngineerRegistration,
                ContractorId = input.ContractorId,
                DocumentId = document?.Id > 0 ? document.Id : (int?)null,
                Observations = input.Observations,
                CreatedAt = now,
                CreatedBy = User.Login()
            };
            _db.Certificates.Add(certificate);

            if (input.Outcome == CertificateOutcome.RemedialRequired)
                certificate.RemedialWorkOrderId = (await RaiseRemedialAsync(property, input, now)).Id;

            await _db.SaveChangesAsync();
            await _audit.WriteAsync(User.Login(), "Certificate", certificate.CertificateNumber, "Recorded", new { property.Reference, input.Type, input.Outcome });
            TempData["Message"] = $"Certificate recorded, valid until {certificate.ExpiresOn:dd/MM/yyyy}."
                + (input.Outcome == CertificateOutcome.RemedialRequired ? " A remedial work order has been raised." : "");
            return RedirectToAction("Details", "Properties", new { id = property.Id });
        }

        private async Task<WorkOrder> RaiseRemedialAsync(Assets.Property property, CertificateUploadInput input, DateTime now)
        {
            var sequence = await _counters.NextAsync("WorkOrder" + now.Year);
            var trade = input.Type == ComplianceType.GasSafety ? Trade.Heating : Trade.Electrical;
            var priority = input.Type == ComplianceType.GasSafety ? RepairPriority.Emergency : RepairPriority.Urgent;
            var tenancy = await _db.Tenancies.Where(t => t.PropertyId == property.Id && t.Status != TenancyStatus.Ended).Select(t => (int?)t.Id).FirstOrDefaultAsync();
            var order = new WorkOrder
            {
                Reference = RepairRules.NextReference(now.Year, sequence),
                PropertyId = property.Id,
                TenancyId = tenancy,
                Channel = ReportChannel.Inspection,
                ReportedBy = input.EngineerName,
                Location = input.Type.ToString(),
                Description = $"Remedial work from {input.Type} certificate {input.CertificateNumber}: {input.Observations}",
                Trade = trade,
                Priority = priority,
                Status = WorkOrderStatus.Reported,
                ReportedAt = now,
                TargetDate = _rules.TargetDate(priority, now),
                ContractorId = input.ContractorId,
                CreatedAt = now,
                CreatedBy = User.Login()
            };
            order.Events.Add(new WorkOrderEvent { At = now, Actor = User.Login(), NewStatus = WorkOrderStatus.Reported, Text = "Raised from certificate " + input.CertificateNumber });
            if (input.ContractorId.HasValue)
            {
                order.AssignedAt = now;
                RepairRules.Move(order, WorkOrderStatus.Assigned, User.Login(), now, "Assigned to the inspecting contractor");
            }
            _db.WorkOrders.Add(order);
            return order;
        }
    }
}
