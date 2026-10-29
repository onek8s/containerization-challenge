using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Compliance;
using Fabrikam.Housing.ContractorApi.Infrastructure;
using Fabrikam.Housing.ContractorApi.Models;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Documents;
using Fabrikam.Housing.Integration.DocumentStore;
using Fabrikam.Housing.Repairs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fabrikam.Housing.ContractorApi.Controllers
{
    [ApiController]
    [Route("api/v1/certificates")]
    [Authorize(Policy = Startup.ContractorPolicy)]
    public class CertificatesController : ControllerBase
    {
        private readonly HousingDbContext _db;
        private readonly CurrentContractor _contractor;
        private readonly IClock _clock;
        private readonly RepairRules _rules;
        private readonly CounterService _counters;
        private readonly IDocumentServiceClient _documents;
        private readonly AuditWriter _audit;
        private readonly IMapper _mapper;

        public CertificatesController(HousingDbContext db, CurrentContractor contractor, IClock clock, RepairRules rules,
            CounterService counters, IDocumentServiceClient documents, AuditWriter audit, IMapper mapper)
        {
            _db = db;
            _contractor = contractor;
            _clock = clock;
            _rules = rules;
            _counters = counters;
            _documents = documents;
            _audit = audit;
            _mapper = mapper;
        }

        [HttpPost]
        [RequestSizeLimit(12 * 1024 * 1024)]
        public async Task<ActionResult<CertificateModel>> Upload([FromForm] CertificateRequest request)
        {
            var contractor = await _contractor.GetAsync();
            if (request.Type == ComplianceType.GasSafety)
            {
                if (!contractor.GasSafeRegistered)
                    throw new BusinessRuleException("GAS_SAFE", "Your company is not recorded as Gas Safe registered with Fabrikam Homes.");
                ComplianceRules.ValidateGasSafeNumber(request.EngineerRegistration);
            }

            var property = await _db.Properties.SingleOrDefaultAsync(p => p.Reference == request.PropertyReference);
            if (property == null)
                throw new BusinessRuleException("PROPERTY", "Property " + request.PropertyReference + " not found.");

            var previous = await _db.Certificates
                .Where(c => c.PropertyId == property.Id && c.Type == request.Type && !c.Superseded).ToListAsync();
            if (previous.Any(c => c.InspectedOn.Date > request.InspectedOn.Date))
                throw new BusinessRuleException("CERT_OLDER", "A more recent certificate is already held for this property.");

            DocumentRecord document;
            using (var stream = request.File.OpenReadStream())
            {
                document = await _documents.UploadAsync(new DocumentUpload
                {
                    Category = DocumentCategory.Certificate,
                    Title = (request.Type == ComplianceType.GasSafety ? "Gas safety record " : "EICR ") + request.CertificateNumber,
                    FileName = Path.GetFileName(request.File.FileName),
                    ContentType = "application/pdf",
                    PropertyId = property.Id,
                    ContractorId = contractor.Id,
                    VisibleToTenant = true,
                    UploadedBy = _contractor.ClientId
                }, stream);
            }

            foreach (var p in previous) p.Superseded = true;
            var certificate = new ComplianceCertificate
            {
                PropertyId = property.Id,
                Property = property,
                Type = request.Type,
                CertificateNumber = request.CertificateNumber,
                InspectedOn = request.InspectedOn.Date,
                ExpiresOn = ComplianceRules.ExpiryFor(request.Type, request.InspectedOn.Date),
                Outcome = request.Outcome,
                EngineerName = request.EngineerName,
                EngineerRegistration = request.EngineerRegistration,
                ContractorId = contractor.Id,
                DocumentId = document?.Id > 0 ? document.Id : (int?)null,
                Observations = request.Observations,
                CreatedAt = _clock.Now,
                CreatedBy = _contractor.ClientId
            };
            _db.Certificates.Add(certificate);

            WorkOrder remedial = null;
            if (request.Outcome != CertificateOutcome.Satisfactory)
            {
                remedial = await RaiseRemedialWork(property, request, contractor.Id);
            }
            await _db.SaveChangesAsync();
            if (remedial != null)
            {
                certificate.RemedialWorkOrderId = remedial.Id;
                await _db.SaveChangesAsync();
            }

            await _audit.WriteAsync(_contractor.ClientId, "ComplianceCertificate", certificate.CertificateNumber, "Uploaded",
                new { property.Reference, certificate.Type, certificate.Outcome, certificate.ExpiresOn, Superseded = previous.Count });

            var model = _mapper.Map<CertificateModel>(certificate);
            model.RemedialWorkOrder = remedial?.Reference;
            model.DocumentKey = document?.DocumentKey;
            return StatusCode(201, model);
        }

        private async Task<WorkOrder> RaiseRemedialWork(Assets.Property property, CertificateRequest request, int contractorId)
        {
            var now = _clock.Now;
            var sorCode = request.Type == ComplianceType.GasSafety ? "HT0101" : "EL0205";
            var item = await _db.ScheduleOfRates.SingleAsync(s => s.Code == sorCode);
            var tenancy = await _db.Tenancies.FirstOrDefaultAsync(t => t.PropertyId == property.Id && t.Status != Tenancies.TenancyStatus.Ended);
            var sequence = await _counters.NextAsync("WorkOrder" + now.Year);
            var order = new WorkOrder
            {
                Reference = RepairRules.NextReference(now.Year, sequence),
                PropertyId = property.Id,
                TenancyId = tenancy?.Id,
                Channel = ReportChannel.Inspection,
                ReportedBy = _contractor.ClientId,
                Location = request.Type == ComplianceType.GasSafety ? "Heating and hot water" : "Electrics",
                Description = "Remedial work from certificate " + request.CertificateNumber + ": " + request.Observations,
                Trade = item.Trade,
                Priority = RepairPriority.Urgent,
                Status = WorkOrderStatus.Assigned,
                ReportedAt = now,
                TargetDate = _rules.TargetDate(RepairPriority.Urgent, now),
                ContractorId = contractorId,
                AssignedAt = now,
                CreatedAt = now,
                CreatedBy = _contractor.ClientId
            };
            order.Lines.Add(new WorkOrderLine { SorCode = item.Code, Description = item.Description, Quantity = 1, UnitPrice = item.UnitPrice });
            order.Events.Add(new WorkOrderEvent { At = now, Actor = _contractor.ClientId, NewStatus = WorkOrderStatus.Assigned, Text = "Raised from certificate " + request.CertificateNumber });
            _db.WorkOrders.Add(order);
            return order;
        }
    }
}
