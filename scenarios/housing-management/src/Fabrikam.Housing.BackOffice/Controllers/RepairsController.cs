using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Fabrikam.Housing.BackOffice.Infrastructure;
using Fabrikam.Housing.BackOffice.Models;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Repairs;
using Fabrikam.Housing.Tenancies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fabrikam.Housing.BackOffice.Controllers
{
    public class RepairsController : Controller
    {
        private readonly HousingDbContext _db;
        private readonly RepairRules _rules;
        private readonly CounterService _counters;
        private readonly AuditWriter _audit;
        private readonly IClock _clock;
        private readonly IMapper _mapper;

        public RepairsController(HousingDbContext db, RepairRules rules, CounterService counters, AuditWriter audit, IClock clock, IMapper mapper)
        {
            _db = db;
            _rules = rules;
            _counters = counters;
            _audit = audit;
            _clock = clock;
            _mapper = mapper;
        }

        public async Task<IActionResult> Index(WorkOrderStatus? status, RepairPriority? priority, string patch, bool overdue = false, string q = null)
        {
            var now = _clock.Now;
            var query = _db.WorkOrders.AsNoTracking().Include(w => w.Property).Include(w => w.Contractor).AsQueryable();
            if (status.HasValue) query = query.Where(w => w.Status == status.Value);
            else query = query.Where(w => w.Status != WorkOrderStatus.Completed && w.Status != WorkOrderStatus.Cancelled);
            if (priority.HasValue) query = query.Where(w => w.Priority == priority.Value);
            if (!string.IsNullOrEmpty(patch)) query = query.Where(w => w.Property.PatchCode == patch);
            if (overdue) query = query.Where(w => w.TargetDate < now);
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(w => w.Reference == q.Trim().ToUpper() || w.Property.AddressLine1.Contains(q.Trim()));

            var orders = await query.OrderBy(w => w.Priority).ThenBy(w => w.TargetDate).Take(200).ToListAsync();
            ViewBag.Status = status;
            ViewBag.Priority = priority;
            ViewBag.Patch = patch;
            ViewBag.Overdue = overdue;
            ViewBag.Q = q;
            return View(orders.Select(w => ToItem(w, now)).ToList());
        }

        /// <summary>Repairs reported by tenants or by phone that nobody has looked at yet.</summary>
        public async Task<IActionResult> Triage()
        {
            var now = _clock.Now;
            var orders = await _db.WorkOrders.AsNoTracking().Include(w => w.Property).Include(w => w.Contractor)
                .Where(w => w.Status == WorkOrderStatus.Reported || w.Status == WorkOrderStatus.Triaged)
                .OrderBy(w => w.Priority).ThenBy(w => w.ReportedAt).ToListAsync();
            return View(orders.Select(w => ToItem(w, now)).ToList());
        }

        public IActionResult Board(string patch)
        {
            ViewBag.Patch = patch ?? User.Patch() ?? "ALL";
            ViewBag.LastEventId = RepairsBoardBroadcaster.LastEventId;
            return View();
        }

        public async Task<IActionResult> BoardData(string patch)
        {
            var now = _clock.Now;
            var query = _db.WorkOrders.AsNoTracking().Include(w => w.Property).Include(w => w.Contractor)
                .Where(w => w.Status != WorkOrderStatus.Completed && w.Status != WorkOrderStatus.Cancelled);
            if (!string.IsNullOrEmpty(patch) && patch != "ALL") query = query.Where(w => w.Property.PatchCode == patch);
            var orders = await query.OrderBy(w => w.Priority).ThenBy(w => w.TargetDate).Take(300).ToListAsync();
            return Json(orders.Select(w => new
            {
                id = w.Id,
                reference = w.Reference,
                address = w.Property.AddressLine1,
                patch = w.Property.PatchCode,
                priority = w.Priority.ToString(),
                status = w.Status.ToString(),
                contractor = w.Contractor?.Code,
                target = w.TargetDate.ToString("dd/MM HH:mm"),
                overdue = w.TargetDate < now
            }));
        }

        public async Task<IActionResult> Details(int id)
        {
            var order = await _db.WorkOrders
                .Include(w => w.Property).ThenInclude(p => p.Estate)
                .Include(w => w.Contractor)
                .Include(w => w.Lines).Include(w => w.Appointments).Include(w => w.Events)
                .AsNoTracking().SingleOrDefaultAsync(w => w.Id == id);
            if (order == null) return NotFound();
            return View(await BuildDetailAsync(order));
        }

        [Authorize(Policies.Repairs)]
        public async Task<IActionResult> Raise(int propertyId)
        {
            var property = await _db.Properties.FindAsync(propertyId);
            if (property == null) return NotFound();
            return View(new RaiseWorkOrderInput
            {
                PropertyId = propertyId,
                PropertyAddress = property.SingleLineAddress,
                ReportedBy = User.DisplayName(),
                Sor = await ActiveSorAsync()
            });
        }

        [HttpPost, Authorize(Policies.Repairs)]
        public async Task<IActionResult> Raise(RaiseWorkOrderInput input)
        {
            var property = await _db.Properties.FindAsync(input.PropertyId);
            if (property == null) return NotFound();
            var sor = await _db.ScheduleOfRates.SingleOrDefaultAsync(s => s.Code == input.SorCode && s.Active);
            if (sor == null) ModelState.AddModelError(nameof(input.SorCode), "Choose a Schedule of Rates code.");
            if (!ModelState.IsValid)
            {
                input.PropertyAddress = property.SingleLineAddress;
                input.Sor = await ActiveSorAsync();
                return View(input);
            }

            var now = _clock.Now;
            var priority = input.Priority ?? sor.DefaultPriority;
            var tenancy = await _db.Tenancies.Where(t => t.PropertyId == property.Id && t.Status != TenancyStatus.Ended)
                .OrderByDescending(t => t.StartDate).FirstOrDefaultAsync();
            var sequence = await _counters.NextAsync("WorkOrder" + now.Year);
            var order = new WorkOrder
            {
                Reference = RepairRules.NextReference(now.Year, sequence),
                PropertyId = property.Id,
                TenancyId = tenancy?.Id,
                Channel = ReportChannel.Staff,
                ReportedBy = string.IsNullOrWhiteSpace(input.ReportedBy) ? User.DisplayName() : input.ReportedBy,
                Location = input.Location,
                Description = input.Description,
                Trade = sor.Trade,
                Priority = priority,
                Status = WorkOrderStatus.Reported,
                ReportedAt = now,
                TargetDate = _rules.TargetDate(priority, now),
                CreatedAt = now,
                CreatedBy = User.Login()
            };
            order.Lines.Add(new WorkOrderLine { SorCode = sor.Code, Description = sor.Description, Quantity = input.Quantity, UnitPrice = sor.UnitPrice });
            order.Events.Add(new WorkOrderEvent { At = now, Actor = User.Login(), NewStatus = WorkOrderStatus.Reported, Text = "Raised in the back office" });
            _db.WorkOrders.Add(order);
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(User.Login(), "WorkOrder", order.Reference, "Raised", new { order.Priority, sor.Code });
            TempData["Message"] = $"Work order {order.Reference} raised, target {order.TargetDate:dd/MM/yyyy HH:mm}.";
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }

        [HttpPost, Authorize(Policies.Repairs)]
        public async Task<IActionResult> Assign(int id, int contractorId)
        {
            var order = await LoadTrackedAsync(id);
            if (order == null) return NotFound();
            var contractor = await _db.Contractors.FindAsync(contractorId);
            if (contractor == null || !contractor.Active) return BadRequest();

            return await ChangeAsync(order, () =>
            {
                if (order.Status == WorkOrderStatus.Reported) RepairRules.Move(order, WorkOrderStatus.Triaged, User.Login(), _clock.Now, "Triaged");
                order.ContractorId = contractor.Id;
                order.AssignedAt = _clock.Now;
                RepairRules.Move(order, WorkOrderStatus.Assigned, User.Login(), _clock.Now, "Assigned to " + contractor.Code);
            }, "Assigned to " + contractor.Name);
        }

        [HttpPost, Authorize(Policies.Repairs)]
        public async Task<IActionResult> ChangePriority(int id, RepairPriority priority)
        {
            var order = await LoadTrackedAsync(id);
            if (order == null) return NotFound();
            return await ChangeAsync(order, () =>
            {
                order.Priority = priority;
                order.TargetDate = _rules.TargetDate(priority, order.ReportedAt);
                order.Events.Add(new WorkOrderEvent { At = _clock.Now, Actor = User.Login(), Text = "Priority changed to " + priority });
                if (order.Status == WorkOrderStatus.Reported) RepairRules.Move(order, WorkOrderStatus.Triaged, User.Login(), _clock.Now, "Triaged");
            }, "Priority changed");
        }

        [HttpPost, Authorize(Policies.Repairs)]
        public async Task<IActionResult> Book(int id, DateTime date, AppointmentSlot slot)
        {
            var order = await LoadTrackedAsync(id);
            if (order == null) return NotFound();
            if (!order.ContractorId.HasValue)
            {
                TempData["Error"] = "Assign a contractor before booking an appointment.";
                return RedirectToAction(nameof(Details), new { id });
            }
            return await ChangeAsync(order, () =>
            {
                foreach (var a in order.Appointments.Where(a => !a.Cancelled && a.Outcome == null)) a.Cancelled = true;
                order.Appointments.Add(new Appointment { Date = date.Date, Slot = slot, BookedBy = User.Login(), BookedAt = _clock.Now });
                RepairRules.Move(order, WorkOrderStatus.Appointed, User.Login(), _clock.Now, $"Appointment {date:ddd dd/MM} {slot}");
            }, "Appointment booked");
        }

        [HttpPost, Authorize(Policies.Repairs)]
        public async Task<IActionResult> CancelAppointment(int id, int appointmentId)
        {
            var order = await LoadTrackedAsync(id);
            if (order == null) return NotFound();
            var appointment = order.Appointments.SingleOrDefault(a => a.Id == appointmentId);
            if (appointment == null) return NotFound();
            return await ChangeAsync(order, () =>
            {
                appointment.Cancelled = true;
                order.Events.Add(new WorkOrderEvent { At = _clock.Now, Actor = User.Login(), Text = $"Appointment {appointment.Date:dd/MM} cancelled" });
            }, "Appointment cancelled");
        }

        [HttpPost, Authorize(Policies.Repairs)]
        public async Task<IActionResult> Move(int id, WorkOrderStatus status, string note)
        {
            var order = await LoadTrackedAsync(id);
            if (order == null) return NotFound();
            return await ChangeAsync(order, () =>
            {
                RepairRules.Move(order, status, User.Login(), _clock.Now, note);
                if (status == WorkOrderStatus.Completed) order.CompletedAt = _clock.Now;
                if (status == WorkOrderStatus.NoAccess) order.NoAccessCount++;
            }, "Status changed to " + status);
        }

        [HttpPost, Authorize(Policies.Repairs)]
        public async Task<IActionResult> Recharge(int id, bool recharge, string reason)
        {
            var order = await LoadTrackedAsync(id);
            if (order == null) return NotFound();
            return await ChangeAsync(order, () =>
            {
                order.Recharge = recharge;
                order.RechargeReason = recharge ? reason : null;
                order.Events.Add(new WorkOrderEvent { At = _clock.Now, Actor = User.Login(), Text = recharge ? "Marked as rechargeable: " + reason : "Recharge removed" });
            }, recharge ? "Marked as rechargeable" : "Recharge removed");
        }

        [HttpPost, Authorize(Policies.Repairs)]
        public async Task<IActionResult> Cancel(int id, string reason)
        {
            var order = await LoadTrackedAsync(id);
            if (order == null) return NotFound();
            return await ChangeAsync(order, () =>
            {
                order.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled by repairs desk" : reason;
                RepairRules.Move(order, WorkOrderStatus.Cancelled, User.Login(), _clock.Now, order.CancellationReason);
            }, "Work order cancelled");
        }

        [HttpPost, Authorize(Policies.Repairs)]
        public async Task<IActionResult> AddLine(int id, string sorCode, decimal quantity)
        {
            var order = await LoadTrackedAsync(id);
            if (order == null) return NotFound();
            var sor = await _db.ScheduleOfRates.SingleOrDefaultAsync(s => s.Code == sorCode);
            if (sor == null || quantity <= 0) return BadRequest();
            return await ChangeAsync(order, () =>
            {
                order.Lines.Add(new WorkOrderLine { SorCode = sor.Code, Description = sor.Description, Quantity = quantity, UnitPrice = sor.UnitPrice });
                order.Events.Add(new WorkOrderEvent { At = _clock.Now, Actor = User.Login(), Text = $"Added {quantity} x {sor.Code}" });
            }, "Line added");
        }

        private async Task<IActionResult> ChangeAsync(WorkOrder order, Action change, string message)
        {
            try
            {
                change();
            }
            catch (BusinessRuleException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Details), new { id = order.Id });
            }
            order.ModifiedAt = _clock.Now;
            order.ModifiedBy = User.Login();
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(User.Login(), "WorkOrder", order.Reference, message);
            TempData["Message"] = message + ".";
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }

        private Task<WorkOrder> LoadTrackedAsync(int id)
        {
            return _db.WorkOrders.Include(w => w.Property).Include(w => w.Lines).Include(w => w.Appointments).Include(w => w.Events)
                .SingleOrDefaultAsync(w => w.Id == id);
        }

        private async Task<WorkOrderDetailViewModel> BuildDetailAsync(WorkOrder order)
        {
            var contractors = await _db.Contractors.AsNoTracking().Where(c => c.Active).OrderBy(c => c.Code).ToListAsync();
            var openJobs = await _db.WorkOrders.Where(w => w.ContractorId != null && w.Status != WorkOrderStatus.Completed && w.Status != WorkOrderStatus.Cancelled)
                .GroupBy(w => w.ContractorId.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);

            return new WorkOrderDetailViewModel
            {
                Order = order,
                Tenancy = order.TenancyId.HasValue
                    ? await _db.Tenancies.AsNoTracking().Include(t => t.Tenants).SingleOrDefaultAsync(t => t.Id == order.TenancyId)
                    : null,
                Contractors = contractors.Where(c => c.CoversTrade(order.Trade)).ToList(),
                Suggested = RepairRules.ChooseContractor(contractors, order.Trade, order.Property.PatchCode, order.Priority, openJobs),
                NextStatuses = Enum.GetValues(typeof(WorkOrderStatus)).Cast<WorkOrderStatus>()
                    .Where(s => s != order.Status && RepairRules.CanMove(order.Status, s)).ToList(),
                AppointmentDays = _rules.AppointmentDays(order.Priority == RepairPriority.Emergency ? RepairPriority.Urgent : order.Priority, _clock.Now, 10).ToList(),
                Sor = await ActiveSorAsync(),
                Overdue = order.IsOpen && order.TargetDate < _clock.Now
            };
        }

        private Task<List<ScheduleOfRatesItem>> ActiveSorAsync() =>
            _db.ScheduleOfRates.AsNoTracking().Where(s => s.Active).OrderBy(s => s.Code).ToListAsync();

        private WorkOrderListItem ToItem(WorkOrder w, DateTime now)
        {
            var item = _mapper.Map<WorkOrderListItem>(w);
            item.Overdue = w.IsOpen && w.TargetDate < now;
            return item;
        }
    }
}
