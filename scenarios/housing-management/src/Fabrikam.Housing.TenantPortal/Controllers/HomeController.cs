using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Compliance;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Data.Queries;
using Fabrikam.Housing.Documents;
using Fabrikam.Housing.Rent;
using Fabrikam.Housing.Repairs;
using Fabrikam.Housing.TenantPortal.Models;
using Fabrikam.Housing.TenantPortal.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fabrikam.Housing.TenantPortal.Controllers
{
    public class HomeController : Controller
    {
        private readonly CurrentTenant _current;
        private readonly HousingDbContext _db;
        private readonly RentAccountQueries _rent;
        private readonly IMapper _mapper;
        private readonly IClock _clock;

        public HomeController(CurrentTenant current, HousingDbContext db, RentAccountQueries rent, IMapper mapper, IClock clock)
        {
            _current = current;
            _db = db;
            _rent = rent;
            _mapper = mapper;
            _clock = clock;
        }

        public async Task<IActionResult> Index()
        {
            var tenant = await _current.GetAsync();
            var tenancy = tenant.Tenancy;

            var lastPayment = await _db.RentTransactions.AsNoTracking()
                .Where(t => t.TenancyId == tenancy.Id && t.Amount < 0 && t.Type != TransactionType.Adjustment)
                .OrderByDescending(t => t.PostedOn).FirstOrDefaultAsync();
            var mandate = await _db.Mandates.AsNoTracking()
                .Where(m => m.TenancyId == tenancy.Id && m.Status != MandateStatus.Cancelled && m.Status != MandateStatus.Failed && m.Status != MandateStatus.Expired)
                .OrderByDescending(m => m.Id).FirstOrDefaultAsync();
            var repairs = await _db.WorkOrders.AsNoTracking().Include(w => w.Appointments).Include(w => w.Contractor)
                .Where(w => w.TenancyId == tenancy.Id && w.Status != WorkOrderStatus.Completed && w.Status != WorkOrderStatus.Cancelled)
                .OrderByDescending(w => w.ReportedAt).ToListAsync();
            var gas = await _db.Certificates.AsNoTracking()
                .Where(c => c.PropertyId == tenancy.PropertyId && c.Type == ComplianceType.GasSafety && !c.Superseded)
                .OrderByDescending(c => c.InspectedOn).FirstOrDefaultAsync();
            var since = _clock.Today.AddDays(-30);

            var model = new DashboardViewModel
            {
                FirstName = tenant.FirstName,
                TenancyReference = tenancy.Reference,
                Address = tenancy.Property.SingleLineAddress,
                Balance = await _rent.BalanceAsync(tenancy.Id),
                WeeklyCharge = tenancy.WeeklyCharge,
                LastPaymentDate = lastPayment?.PostedOn,
                LastPaymentAmount = lastPayment == null ? (decimal?)null : -lastPayment.Amount,
                DirectDebit = mandate == null ? null : _mapper.Map<MandateViewModel>(mandate),
                OpenRepairs = _mapper.Map<System.Collections.Generic.List<RepairSummary>>(repairs),
                GasCheckDue = tenancy.Property.HasGasSupply ? gas?.ExpiresOn : null,
                NewLetters = await _db.Documents.CountAsync(d => d.TenancyId == tenancy.Id && d.VisibleToTenant && !d.Deleted
                    && (d.Category == DocumentCategory.Letter || d.Category == DocumentCategory.Notice || d.Category == DocumentCategory.Statement) && d.UploadedAt >= since),
                UnansweredMessages = await _db.Enquiries.CountAsync(e => e.TenancyId == tenancy.Id && e.RespondedAt == null)
            };
            return View(model);
        }

        [AllowAnonymous]
        public IActionResult AccessDenied() => View();

        [AllowAnonymous]
        public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

        [HttpPost]
        public IActionResult SignOut()
        {
            return SignOut(new AuthenticationProperties { RedirectUri = "/" },
                CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme);
        }
    }
}
