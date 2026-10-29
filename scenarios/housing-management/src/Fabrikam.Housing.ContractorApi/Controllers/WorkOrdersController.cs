using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Communications;
using Fabrikam.Housing.ContractorApi.Infrastructure;
using Fabrikam.Housing.ContractorApi.Models;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Documents;
using Fabrikam.Housing.Integration.DocumentStore;
using Fabrikam.Housing.Integration.Messaging;
using Fabrikam.Housing.Repairs;
using Fabrikam.Housing.Tenancies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fabrikam.Housing.ContractorApi.Controllers
{
    [ApiController]
    [Route("api/v1/workorders")]
    [Authorize(Policy = Startup.ContractorPolicy)]
    public class WorkOrdersController : ControllerBase
    {
        private const long MaxPhotoBytes = 10 * 1024 * 1024;
        private static readonly string[] PhotoTypes = { "image/jpeg", "image/png" };

        private readonly HousingDbContext _db;
        private readonly CurrentContractor _contractor;
        private readonly IClock _clock;
        private readonly WorkingCalendar _calendar;
        private readonly ISmsSender _sms;
        private readonly IDocumentServiceClient _documents;
        private readonly ILogger<WorkOrdersController> _logger;

        public WorkOrdersController(HousingDbContext db, CurrentContractor contractor, IClock clock, WorkingCalendar calendar,
            ISmsSender sms, IDocumentServiceClient documents, ILogger<WorkOrdersController> logger)
        {
            _db = db;
            _contractor = contractor;
            _clock = clock;
            _calendar = calendar;
            _sms = sms;
            _documents = documents;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<WorkOrderSummary>>> List(WorkOrderStatus? status, DateTime? since, int page = 1, int pageSize = 50)
        {
            var contractor = await _contractor.GetAsync();
            page = Math.Max(1, page);
            pageSize = Math.Min(Math.Max(1, pageSize), 100);

            var query = _db.WorkOrders.AsNoTracking().Include(w => w.Property).Where(w => w.ContractorId == contractor.Id);
            if (status.HasValue) query = query.Where(w => w.Status == status.Value);
            else query = query.Where(w => w.Status != WorkOrderStatus.Completed && w.Status != WorkOrderStatus.Cancelled);
            if (since.HasValue) query = query.Where(w => w.AssignedAt >= since.Value || w.ModifiedAt >= since.Value);

            var total = await query.CountAsync();
            var items = await query.OrderBy(w => w.TargetDate).ThenBy(w => w.Reference)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return new PagedResult<WorkOrderSummary> { Page = page, PageSize = pageSize, Total = total, Items = items.Select(Summary).ToList() };
        }

        [HttpGet("{reference}")]
        public async Task<ActionResult<WorkOrderDetail>> Get(string reference)
        {
            var order = await Load(reference);
            if (order == null) return NotFound();

            var detail = new WorkOrderDetail();
            Fill(detail, order);
            detail.Uprn = order.Property.Uprn.ToString();
            detail.Latitude = order.Property.Latitude;
            detail.Longitude = order.Property.Longitude;
            detail.AssignedAt = order.AssignedAt;
            detail.AcceptedAt = order.AcceptedAt;
            detail.CompletedAt = order.CompletedAt;
            detail.NoAccessCount = order.NoAccessCount;
            detail.AsbestosPresent = order.Property.AsbestosPresent;
            detail.Lines = order.Lines.Select(l => new LineModel { SorCode = l.SorCode, Description = l.Description, Quantity = l.Quantity, UnitPrice = l.UnitPrice, LineTotal = l.LineTotal, AddedByContractor = l.AddedByContractor }).ToList();
            detail.Appointments = order.Appointments.OrderBy(a => a.Date).Select(a => new AppointmentModel { Date = a.Date, Slot = a.Slot, Cancelled = a.Cancelled, Outcome = a.Outcome }).ToList();

            var lead = await LeadTenant(order);
            if (lead != null)
            {
                detail.Contact = new ContactModel
                {
                    Name = lead.FullName,
                    MobilePhone = lead.MobilePhone,
                    Note = lead.Vulnerable ? "Contact before visit" : null
                };
            }
            return detail;
        }

        [HttpPost("{reference}/accept")]
        public async Task<IActionResult> Accept(string reference)
        {
            var order = await Load(reference, tracking: true);
            if (order == null) return NotFound();
            RepairRules.Move(order, WorkOrderStatus.Accepted, _contractor.ClientId, _clock.Now);
            order.AcceptedAt = _clock.Now;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("{reference}/reject")]
        public async Task<IActionResult> Reject(string reference, RejectRequest request)
        {
            var order = await Load(reference, tracking: true);
            if (order == null) return NotFound();
            if (order.Status != WorkOrderStatus.Assigned)
                throw new BusinessRuleException("WO_REJECT", "Only work orders that have not been accepted can be rejected.");
            var code = _contractor.Code;
            RepairRules.Move(order, WorkOrderStatus.Triaged, _contractor.ClientId, _clock.Now, "Rejected by " + code + ": " + request.Reason);
            order.ContractorId = null;
            order.AssignedAt = null;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Work order {Reference} rejected by {Contractor}", order.Reference, code);
            return NoContent();
        }

        [HttpPost("{reference}/appointments")]
        public async Task<IActionResult> Book(string reference, AppointmentRequest request)
        {
            var order = await Load(reference, tracking: true);
            if (order == null) return NotFound();
            var date = request.Date.Date;
            if (date < _clock.Today)
                throw new BusinessRuleException("APPT_PAST", "Appointments cannot be booked in the past.");
            if (!_calendar.IsWorkingDay(date) && order.Priority != RepairPriority.Emergency)
                throw new BusinessRuleException("APPT_DAY", date.ToString("dddd d MMMM yyyy") + " is not a working day.");

            foreach (var existing in order.Appointments.Where(a => !a.Cancelled && a.Outcome == null))
                existing.Cancelled = true;
            order.Appointments.Add(new Appointment { Date = date, Slot = request.Slot, BookedAt = _clock.Now, BookedBy = _contractor.ClientId });
            if (order.Status == WorkOrderStatus.Assigned)
            {
                order.AcceptedAt = _clock.Now;
                RepairRules.Move(order, WorkOrderStatus.Accepted, _contractor.ClientId, _clock.Now, "Accepted when booking");
            }
            RepairRules.Move(order, WorkOrderStatus.Appointed, _contractor.ClientId, _clock.Now, $"Appointment {date:dd/MM/yyyy} {request.Slot}");
            await _db.SaveChangesAsync();

            var lead = await LeadTenant(order);
            if (lead != null && lead.ContactPreference == ContactPreference.Sms && !string.IsNullOrEmpty(lead.MobilePhone))
                await TextTenant(order, lead, date, request.Slot);
            return NoContent();
        }

        [HttpPost("{reference}/no-access")]
        public async Task<IActionResult> NoAccess(string reference, NoAccessRequest request)
        {
            var order = await Load(reference, tracking: true);
            if (order == null) return NotFound();
            var appointment = order.Appointments.Where(a => !a.Cancelled && a.Outcome == null).OrderByDescending(a => a.Date).FirstOrDefault();
            if (appointment != null) appointment.Outcome = "No access - " + request.Outcome;
            order.NoAccessCount++;
            RepairRules.Move(order, WorkOrderStatus.NoAccess, _contractor.ClientId, _clock.Now, request.Outcome);
            if (order.NoAccessCount >= 3)
                RepairRules.Move(order, WorkOrderStatus.OnHold, _contractor.ClientId, _clock.Now, "Third no access: referred to housing officer");
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("{reference}/complete")]
        public async Task<IActionResult> Complete(string reference, CompleteRequest request)
        {
            var order = await Load(reference, tracking: true);
            if (order == null) return NotFound();

            var codes = request.ExtraLines.Select(l => l.SorCode).Distinct().ToList();
            var rates = await _db.ScheduleOfRates.Where(s => codes.Contains(s.Code) && s.Active).ToDictionaryAsync(s => s.Code);
            foreach (var line in request.ExtraLines)
            {
                if (!rates.TryGetValue(line.SorCode, out var item))
                    throw new BusinessRuleException("SOR_CODE", "Unknown Schedule of Rates code " + line.SorCode + ".");
                order.Lines.Add(new WorkOrderLine { SorCode = item.Code, Description = item.Description, Quantity = line.Quantity, UnitPrice = item.UnitPrice, AddedByContractor = true });
            }

            var completedAt = request.CompletedAt ?? _clock.Now;
            if (completedAt > _clock.Now.AddMinutes(5))
                throw new BusinessRuleException("WO_COMPLETED_AT", "Completion time cannot be in the future.");
            RepairRules.Move(order, WorkOrderStatus.Completed, _contractor.ClientId, _clock.Now, request.Notes);
            order.CompletedAt = completedAt;
            order.CompletionNotes = request.Notes;
            order.LabourHours = request.LabourHours;
            foreach (var a in order.Appointments.Where(a => !a.Cancelled && a.Outcome == null)) a.Outcome = "Completed";
            if (request.SuggestRecharge)
            {
                // The repairs team decides whether the tenant is actually recharged.
                order.Events.Add(new WorkOrderEvent { At = _clock.Now, Actor = _contractor.ClientId, Text = "Recharge suggested: " + request.RechargeReason });
                order.RechargeReason = request.RechargeReason;
            }
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("{reference}/photos")]
        [RequestSizeLimit(60 * 1024 * 1024)]
        public async Task<ActionResult<IList<PhotoResult>>> Photos(string reference, [FromForm] IFormFileCollection files)
        {
            var order = await Load(reference);
            if (order == null) return NotFound();
            files = files != null && files.Count > 0 ? files : Request.Form.Files;
            if (files.Count == 0) return BadRequest(new ValidationProblemDetails { Detail = "Attach at least one photo." });
            if (files.Count > 6) return BadRequest(new ValidationProblemDetails { Detail = "At most six photos per request." });
            foreach (var f in files)
            {
                if (!PhotoTypes.Contains(f.ContentType?.ToLowerInvariant()))
                    return BadRequest(new ValidationProblemDetails { Detail = f.FileName + ": only JPEG and PNG photos are accepted." });
                if (f.Length > MaxPhotoBytes)
                    return BadRequest(new ValidationProblemDetails { Detail = f.FileName + " is larger than 10 MB." });
            }

            var contractor = await _contractor.GetAsync();
            var results = new List<PhotoResult>();
            foreach (var f in files)
            {
                using (var stream = f.OpenReadStream())
                {
                    var record = await _documents.UploadAsync(new DocumentUpload
                    {
                        Category = DocumentCategory.RepairPhoto,
                        Title = "Repair photo " + order.Reference,
                        FileName = Path.GetFileName(f.FileName),
                        ContentType = f.ContentType,
                        PropertyId = order.PropertyId,
                        TenancyId = order.TenancyId,
                        WorkOrderId = order.Id,
                        ContractorId = contractor.Id,
                        UploadedBy = _contractor.ClientId
                    }, stream);
                    results.Add(new PhotoResult { DocumentKey = record.DocumentKey, FileName = record.FileName });
                }
            }
            return results;
        }

        private async Task<WorkOrder> Load(string reference, bool tracking = false)
        {
            var contractor = await _contractor.GetAsync();
            IQueryable<WorkOrder> query = _db.WorkOrders.Include(w => w.Property).Include(w => w.Lines).Include(w => w.Appointments);
            if (!tracking) query = query.AsNoTracking();
            return await query.SingleOrDefaultAsync(w => w.Reference == reference && w.ContractorId == contractor.Id);
        }

        private async Task<Tenant> LeadTenant(WorkOrder order)
        {
            if (!order.TenancyId.HasValue) return null;
            return await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.TenancyId == order.TenancyId && t.IsLeadTenant);
        }

        private async Task TextTenant(WorkOrder order, Tenant lead, DateTime date, AppointmentSlot slot)
        {
            var slotText = slot == AppointmentSlot.Morning ? "8am-12pm" : slot == AppointmentSlot.Afternoon ? "12pm-5pm" : slot == AppointmentSlot.SchoolRun ? "9.30am-2.30pm" : "8am-5pm";
            var text = SmsTemplates.Truncate(SmsTemplates.RepairAppointment(order.Reference, date, slotText));
            var log = new CommunicationLog { TenancyId = order.TenancyId, Channel = Channel.Sms, TemplateCode = "RepairAppointment", Recipient = lead.MobilePhone, Body = text, CreatedAt = _clock.Now };
            try
            {
                log.ProviderReference = await _sms.SendAsync(lead.MobilePhone, text);
                log.Status = CommunicationStatus.Sent;
                log.SentAt = _clock.Now;
            }
            catch (Exception ex)
            {
                // The booking stands even when the text cannot be sent.
                _logger.LogWarning(ex, "Appointment SMS for {Reference} failed", order.Reference);
                log.Status = CommunicationStatus.Failed;
                log.Error = ex.Message;
            }
            _db.Communications.Add(log);
            await _db.SaveChangesAsync();
        }

        private static WorkOrderSummary Summary(WorkOrder w)
        {
            var s = new WorkOrderSummary();
            Fill(s, w);
            return s;
        }

        private static void Fill(WorkOrderSummary s, WorkOrder w)
        {
            s.Reference = w.Reference;
            s.Status = w.Status;
            s.Priority = w.Priority;
            s.Trade = w.Trade;
            s.Location = w.Location;
            s.Description = w.Description;
            s.ReportedAt = w.ReportedAt;
            s.TargetDate = w.TargetDate;
            s.PropertyReference = w.Property.Reference;
            s.Address = w.Property.SingleLineAddress;
            s.Postcode = w.Property.Postcode;
        }
    }
}
