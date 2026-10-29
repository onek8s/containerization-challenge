using System;
using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Communications;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Documents;
using Fabrikam.Housing.Integration.DocumentStore;
using Fabrikam.Housing.Repairs;
using Fabrikam.Housing.Tenancies;
using Fabrikam.Housing.TenantPortal.Models;
using Fabrikam.Housing.TenantPortal.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Fabrikam.Housing.TenantPortal.Controllers
{
    /// <summary>
    /// Report a repair: area, problem, details, photos, appointment, confirm. Emergencies skip the appointment
    /// step because the contractor attends within 24 hours.
    /// </summary>
    public class ReportRepairController : Controller
    {
        private readonly CurrentTenant _current;
        private readonly HousingDbContext _db;
        private readonly RepairWizardStore _store;
        private readonly RepairRules _rules;
        private readonly CounterService _counters;
        private readonly IDocumentServiceClient _documents;
        private readonly TenantNotifier _notifier;
        private readonly AuditWriter _audit;
        private readonly IMemoryCache _cache;
        private readonly IClock _clock;
        private readonly ILogger<ReportRepairController> _logger;

        public ReportRepairController(CurrentTenant current, HousingDbContext db, RepairWizardStore store, RepairRules rules,
            CounterService counters, IDocumentServiceClient documents, TenantNotifier notifier, AuditWriter audit, IMemoryCache cache,
            IClock clock, ILogger<ReportRepairController> logger)
        {
            _current = current;
            _db = db;
            _store = store;
            _rules = rules;
            _counters = counters;
            _documents = documents;
            _notifier = notifier;
            _audit = audit;
            _cache = cache;
            _clock = clock;
            _logger = logger;
        }

        private async Task<ScheduleOfRatesItem> SorAsync(string code)
        {
            var items = await _cache.GetOrCreateAsync("sor-items", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return _db.ScheduleOfRates.AsNoTracking().Where(s => s.Active).ToListAsync();
            });
            return items.FirstOrDefault(s => s.Code == code);
        }

        private async Task<RepairPriority> PriorityAsync(RepairFinder.Option option) =>
            (await SorAsync(option.SorCode))?.DefaultPriority ?? RepairPriority.Routine;

        [HttpGet]
        public IActionResult Index()
        {
            _store.Clear();
            return RedirectToAction(nameof(Area));
        }

        [HttpGet]
        public IActionResult Area()
        {
            var draft = _store.Load();
            return View(new AreaStepViewModel { Areas = RepairFinder.Areas, Area = draft.Area });
        }

        [HttpPost]
        public IActionResult Area(AreaStepViewModel model)
        {
            if (!RepairFinder.Areas.Contains(model.Area)) ModelState.AddModelError(nameof(model.Area), "Choose where the problem is.");
            if (!ModelState.IsValid)
            {
                model.Areas = RepairFinder.Areas;
                return View(model);
            }
            var draft = _store.Load();
            if (draft.Area != model.Area) draft.OptionKey = null;
            draft.Area = model.Area;
            _store.Save(draft);
            return RedirectToAction(nameof(Problem));
        }

        [HttpGet]
        public IActionResult Problem()
        {
            var draft = _store.Load();
            if (draft.Area == null) return RedirectToAction(nameof(Area));
            return View(new ProblemStepViewModel { Area = draft.Area, Options = RepairFinder.ForArea(draft.Area).ToList(), OptionKey = draft.OptionKey });
        }

        [HttpPost]
        public IActionResult Problem(ProblemStepViewModel model)
        {
            var draft = _store.Load();
            var option = RepairFinder.Find(model.OptionKey);
            if (option == null || option.Area != draft.Area) ModelState.AddModelError(nameof(model.OptionKey), "Choose the problem that best matches.");
            if (!ModelState.IsValid)
            {
                model.Area = draft.Area;
                model.Options = RepairFinder.ForArea(draft.Area).ToList();
                return View(model);
            }
            draft.OptionKey = option.Key;
            _store.Save(draft);
            return RedirectToAction(nameof(Details));
        }

        [HttpGet]
        public async Task<IActionResult> Details()
        {
            var draft = _store.Load();
            var option = RepairFinder.Find(draft.OptionKey);
            if (option == null) return RedirectToAction(nameof(Problem));
            return View(new DetailsStepViewModel { Option = option, Priority = await PriorityAsync(option), Location = draft.Location ?? draft.Area, Description = draft.Description ?? option.Text });
        }

        [HttpPost]
        public async Task<IActionResult> Details(DetailsStepViewModel model)
        {
            var draft = _store.Load();
            var option = RepairFinder.Find(draft.OptionKey);
            if (option == null) return RedirectToAction(nameof(Problem));
            if (!ModelState.IsValid)
            {
                model.Option = option;
                model.Priority = await PriorityAsync(option);
                return View(model);
            }
            draft.Location = model.Location.Trim();
            draft.Description = model.Description.Trim();
            _store.Save(draft);
            return RedirectToAction(nameof(Photos));
        }

        [HttpGet]
        public IActionResult Photos()
        {
            var draft = _store.Load();
            var option = RepairFinder.Find(draft.OptionKey);
            if (option == null || draft.Description == null) return RedirectToAction(nameof(Details));
            return View(new PhotosStepViewModel { PhotoSuggested = option.AskForPhoto, Photos = draft.Photos, MaxPhotos = RepairWizardStore.MaxPhotos });
        }

        [HttpPost]
        [RequestSizeLimit(30 * 1024 * 1024)]
        public async Task<IActionResult> Photos(IFormFile photo, string remove, string next)
        {
            var draft = _store.Load();
            if (!string.IsNullOrEmpty(remove))
            {
                _store.RemovePhoto(draft, remove);
                return RedirectToAction(nameof(Photos));
            }
            if (photo != null)
            {
                if (draft.Photos.Count >= RepairWizardStore.MaxPhotos)
                    TempData["Error"] = $"You can add up to {RepairWizardStore.MaxPhotos} photos.";
                else if (!RepairWizardStore.IsAllowedPhoto(photo))
                    TempData["Error"] = "Photos must be JPEG, PNG or GIF files smaller than 8 MB.";
                else
                    await _store.AddPhotoAsync(draft, photo);
                if (string.IsNullOrEmpty(next)) return RedirectToAction(nameof(Photos));
            }

            var option = RepairFinder.Find(draft.OptionKey);
            if (option != null && await PriorityAsync(option) == RepairPriority.Emergency) return RedirectToAction(nameof(Confirm));
            return RedirectToAction(nameof(Appointment));
        }

        [HttpGet]
        public async Task<IActionResult> Appointment()
        {
            var draft = _store.Load();
            var option = RepairFinder.Find(draft.OptionKey);
            if (option == null || draft.Description == null) return RedirectToAction(nameof(Details));
            var priority = await PriorityAsync(option);
            if (priority == RepairPriority.Emergency) return RedirectToAction(nameof(Confirm));
            return View(new AppointmentStepViewModel
            {
                Priority = priority,
                Days = _rules.AppointmentDays(priority, _clock.Now, 10).ToList(),
                Date = draft.AppointmentDate,
                Slot = draft.Slot
            });
        }

        [HttpPost]
        public async Task<IActionResult> Appointment(AppointmentStepViewModel model)
        {
            var draft = _store.Load();
            var option = RepairFinder.Find(draft.OptionKey);
            if (option == null) return RedirectToAction(nameof(Area));
            var priority = await PriorityAsync(option);
            var days = _rules.AppointmentDays(priority, _clock.Now, 10).ToList();
            if (model.Date.HasValue && !days.Contains(model.Date.Value.Date)) ModelState.AddModelError(nameof(model.Date), "Choose one of the days shown.");
            if (!ModelState.IsValid)
            {
                model.Priority = priority;
                model.Days = days;
                return View(model);
            }
            draft.AppointmentDate = model.Date.Value.Date;
            draft.Slot = model.Slot;
            _store.Save(draft);
            return RedirectToAction(nameof(Confirm));
        }

        [HttpGet]
        public async Task<IActionResult> Confirm()
        {
            var draft = _store.Load();
            var option = RepairFinder.Find(draft.OptionKey);
            if (option == null || draft.Description == null) return RedirectToAction(nameof(Area));
            var priority = await PriorityAsync(option);
            if (priority != RepairPriority.Emergency && !draft.AppointmentDate.HasValue) return RedirectToAction(nameof(Appointment));
            var tenancy = await _current.TenancyAsync();
            return View(new ConfirmStepViewModel
            {
                Draft = draft,
                Option = option,
                Priority = priority,
                TargetDate = _rules.TargetDate(priority, _clock.Now),
                Address = tenancy.Property.SingleLineAddress
            });
        }

        [HttpPost]
        [ActionName("Confirm")]
        public async Task<IActionResult> Submit()
        {
            var draft = _store.Load();
            var option = RepairFinder.Find(draft.OptionKey);
            if (option == null || draft.Description == null) return RedirectToAction(nameof(Area));
            var tenant = await _current.GetAsync();
            var sor = await SorAsync(option.SorCode);
            var priority = sor?.DefaultPriority ?? RepairPriority.Routine;
            if (priority != RepairPriority.Emergency && !draft.AppointmentDate.HasValue) return RedirectToAction(nameof(Appointment));

            var now = _clock.Now;
            var sequence = await _counters.NextAsync("WorkOrder" + now.Year);
            var order = new WorkOrder
            {
                Reference = RepairRules.NextReference(now.Year, sequence),
                PropertyId = tenant.Tenancy.PropertyId,
                TenancyId = tenant.TenancyId,
                Channel = ReportChannel.Portal,
                ReportedBy = tenant.FullName,
                Location = draft.Location,
                Description = option.Text + ". " + draft.Description,
                Trade = sor?.Trade ?? Trade.General,
                Priority = priority,
                Status = WorkOrderStatus.Reported,
                ReportedAt = now,
                TargetDate = _rules.TargetDate(priority, now),
                CreatedAt = now,
                CreatedBy = User.Identity.Name
            };
            if (order.Description.Length > 2000) order.Description = order.Description.Substring(0, 2000);
            if (sor != null)
                order.Lines.Add(new WorkOrderLine { SorCode = sor.Code, Description = sor.Description, Quantity = 1, UnitPrice = sor.UnitPrice });
            order.Events.Add(new WorkOrderEvent { At = now, Actor = tenant.FullName, NewStatus = WorkOrderStatus.Reported, Text = "Reported on the tenant portal" });
            if (draft.AppointmentDate.HasValue)
            {
                // The tenant's preferred slot; the contractor confirms it when accepting the job.
                order.Appointments.Add(new Appointment { Date = draft.AppointmentDate.Value, Slot = draft.Slot ?? AppointmentSlot.AllDay, BookedBy = User.Identity.Name, BookedAt = now });
            }
            _db.WorkOrders.Add(order);
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(User.Identity.Name, "WorkOrder", order.Reference, "ReportedOnPortal", new { option.Key, priority });

            var result = new RepairReportedViewModel { Reference = order.Reference, Priority = priority, TargetDate = order.TargetDate, AppointmentDate = draft.AppointmentDate, Slot = draft.Slot };
            foreach (var photo in draft.Photos)
            {
                try
                {
                    using (var stream = _store.OpenPhoto(photo))
                    {
                        await _documents.UploadAsync(new DocumentUpload
                        {
                            Category = DocumentCategory.RepairPhoto,
                            Title = "Photo from tenant - " + order.Reference,
                            FileName = photo.FileName,
                            ContentType = photo.ContentType,
                            PropertyId = order.PropertyId,
                            TenancyId = order.TenancyId,
                            WorkOrderId = order.Id,
                            VisibleToTenant = true,
                            UploadedBy = User.Identity.Name
                        }, stream);
                    }
                    result.PhotosUploaded++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Photo {File} for {WorkOrder} could not be stored", photo.FileName, order.Reference);
                    result.PhotosFailed++;
                }
            }
            if (result.PhotosFailed > 0)
            {
                order.Events.Add(new WorkOrderEvent { At = _clock.Now, Actor = "portal", Text = $"{result.PhotosFailed} photo(s) from the tenant could not be stored" });
                await _db.SaveChangesAsync();
            }
            _store.Clear();

            var when = draft.AppointmentDate.HasValue
                ? $"{draft.AppointmentDate:dddd d MMMM} ({SlotText.For(draft.Slot)})"
                : "within 24 hours";
            await _notifier.ConfirmAsync(tenant, "REPAIR_REPORTED",
                draft.AppointmentDate.HasValue
                    ? SmsTemplates.RepairAppointment(order.Reference, draft.AppointmentDate.Value, SlotText.For(draft.Slot))
                    : $"Fabrikam Homes: we have your emergency repair {order.Reference}. A contractor will attend within 24 hours.",
                "Repair " + order.Reference + " reported",
                $"<p>Dear {System.Net.WebUtility.HtmlEncode(tenant.FirstName)},</p><p>Thank you for reporting a repair: {System.Net.WebUtility.HtmlEncode(option.Text)}.</p>" +
                $"<p>Your reference is <strong>{order.Reference}</strong>. Requested appointment: {when}.</p><p>Fabrikam Homes Repairs</p>");

            TempData["Reported"] = Newtonsoft.Json.JsonConvert.SerializeObject(result);
            return RedirectToAction(nameof(Done));
        }

        [HttpGet]
        public IActionResult Done()
        {
            var json = TempData["Reported"] as string;
            if (json == null) return RedirectToAction("Index", "Repairs");
            return View(Newtonsoft.Json.JsonConvert.DeserializeObject<RepairReportedViewModel>(json));
        }
    }
}
