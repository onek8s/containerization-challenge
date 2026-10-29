using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Fabrikam.Housing.BackOffice.Infrastructure;
using Fabrikam.Housing.BackOffice.Models;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Data.Queries;
using Fabrikam.Housing.Repairs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fabrikam.Housing.BackOffice.Controllers
{
    public class HomeController : Controller
    {
        private readonly HousingDbContext _db;
        private readonly ReportQueries _reports;
        private readonly IClock _clock;
        private readonly IMapper _mapper;

        public HomeController(HousingDbContext db, ReportQueries reports, IClock clock, IMapper mapper)
        {
            _db = db;
            _reports = reports;
            _clock = clock;
            _mapper = mapper;
        }

        public async Task<IActionResult> Index(string patch)
        {
            patch = patch ?? User.Patch();
            var now = _clock.Now;
            var arrears = await _reports.ArrearsAsync(10m, string.IsNullOrEmpty(patch) ? null : patch);
            var enquiries = await _db.Enquiries.AsNoTracking()
                .Include(e => e.Tenancy).ThenInclude(t => t.Property)
                .Where(e => e.RespondedAt == null && (patch == null || e.AssignedTo == patch))
                .OrderBy(e => e.CreatedAt)
                .Take(10)
                .ToListAsync();
            var emergencies = await _db.WorkOrders.AsNoTracking()
                .Include(w => w.Property).Include(w => w.Contractor)
                .Where(w => w.Priority == RepairPriority.Emergency && w.Status != WorkOrderStatus.Completed && w.Status != WorkOrderStatus.Cancelled)
                .OrderBy(w => w.TargetDate)
                .Take(10)
                .ToListAsync();

            var model = new DashboardViewModel
            {
                Patch = patch,
                Counts = await _reports.DashboardCountsAsync(now),
                TopArrears = arrears.Take(10).ToList(),
                OpenEnquiries = enquiries,
                Emergencies = emergencies.Select(w =>
                {
                    var item = _mapper.Map<WorkOrderListItem>(w);
                    item.Overdue = w.TargetDate < now;
                    return item;
                }).ToList()
            };
            return View(model);
        }

        [AllowAnonymous]
        public IActionResult AccessDenied() => View();

        [AllowAnonymous]
        public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

        [HttpPost]
        public IActionResult Logout()
        {
            return SignOut(new AuthenticationProperties { RedirectUri = "/" },
                CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme);
        }
    }
}
