using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Documents;
using Fabrikam.Housing.Repairs;
using Fabrikam.Housing.TenantPortal.Models;
using Fabrikam.Housing.TenantPortal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fabrikam.Housing.TenantPortal.Controllers
{
    public class RepairsController : Controller
    {
        private static readonly WorkOrderStatus[] CancellableStatuses =
            { WorkOrderStatus.Reported, WorkOrderStatus.Triaged, WorkOrderStatus.Assigned, WorkOrderStatus.Accepted };

        private readonly CurrentTenant _current;
        private readonly HousingDbContext _db;
        private readonly AuditWriter _audit;
        private readonly IMapper _mapper;
        private readonly IClock _clock;

        public RepairsController(CurrentTenant current, HousingDbContext db, AuditWriter audit, IMapper mapper, IClock clock)
        {
            _current = current;
            _db = db;
            _audit = audit;
            _mapper = mapper;
            _clock = clock;
        }

        public async Task<IActionResult> Index()
        {
            var tenancy = await _current.TenancyAsync();
            var orders = await _db.WorkOrders.AsNoTracking().Include(w => w.Appointments).Include(w => w.Contractor)
                .Where(w => w.TenancyId == tenancy.Id)
                .OrderByDescending(w => w.ReportedAt).Take(100).ToListAsync();
            return View(_mapper.Map<List<RepairSummary>>(orders));
        }

        private Task<WorkOrder> FindAsync(int tenancyId, string reference) =>
            _db.WorkOrders.Include(w => w.Appointments).Include(w => w.Events).Include(w => w.Contractor)
                .FirstOrDefaultAsync(w => w.Reference == reference && w.TenancyId == tenancyId);

        public async Task<IActionResult> Details(string id)
        {
            var tenancy = await _current.TenancyAsync();
            var order = await FindAsync(tenancy.Id, id);
            if (order == null) return NotFound();

            var model = _mapper.Map<RepairDetailViewModel>(order);
            model.CanCancel = CancellableStatuses.Contains(order.Status) && !order.Appointments.Any(a => !a.Cancelled && a.Date >= _clock.Today);
            model.CanRate = order.Status == WorkOrderStatus.Completed && !order.SatisfactionScore.HasValue;
            model.Photos = _mapper.Map<List<DocumentViewModel>>(await _db.Documents.AsNoTracking()
                .Where(d => d.WorkOrderId == order.Id && d.VisibleToTenant && !d.Deleted).ToListAsync());
            // Contractor notes are internal; tenants only see status changes.
            model.Events = model.Events.Where(e => e.NewStatus.HasValue).ToList();
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Cancel(string id, string reason)
        {
            var tenancy = await _current.TenancyAsync();
            var order = await FindAsync(tenancy.Id, id);
            if (order == null) return NotFound();
            if (!CancellableStatuses.Contains(order.Status) || order.Appointments.Any(a => !a.Cancelled && a.Date >= _clock.Today))
            {
                TempData["Error"] = "This repair already has an appointment. Please call us to cancel it.";
                return RedirectToAction(nameof(Details), new { id });
            }
            order.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled by tenant" : "Cancelled by tenant: " + reason.Trim();
            RepairRules.Move(order, WorkOrderStatus.Cancelled, User.Identity.Name, _clock.Now, order.CancellationReason);
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(User.Identity.Name, "WorkOrder", order.Reference, "CancelledByTenant", new { reason });
            TempData["Message"] = $"Repair {order.Reference} has been cancelled.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> Rate(string id, int score)
        {
            var tenancy = await _current.TenancyAsync();
            var order = await FindAsync(tenancy.Id, id);
            if (order == null) return NotFound();
            if (order.Status == WorkOrderStatus.Completed && !order.SatisfactionScore.HasValue && score >= 1 && score <= 5)
            {
                order.SatisfactionScore = score;
                order.Events.Add(new WorkOrderEvent { At = _clock.Now, Actor = User.Identity.Name, Text = $"Tenant rated the repair {score} out of 5" });
                await _db.SaveChangesAsync();
                TempData["Message"] = "Thank you for your feedback.";
            }
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
