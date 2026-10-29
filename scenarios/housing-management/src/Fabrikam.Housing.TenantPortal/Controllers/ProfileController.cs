using System.Threading.Tasks;
using AutoMapper;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Tenancies;
using Fabrikam.Housing.TenantPortal.Models;
using Fabrikam.Housing.TenantPortal.Services;
using Microsoft.AspNetCore.Mvc;

namespace Fabrikam.Housing.TenantPortal.Controllers
{
    public class ProfileController : Controller
    {
        private readonly CurrentTenant _current;
        private readonly HousingDbContext _db;
        private readonly AuditWriter _audit;
        private readonly IMapper _mapper;
        private readonly IClock _clock;

        public ProfileController(CurrentTenant current, HousingDbContext db, AuditWriter audit, IMapper mapper, IClock clock)
        {
            _current = current;
            _db = db;
            _audit = audit;
            _mapper = mapper;
            _clock = clock;
        }

        [HttpGet]
        public async Task<IActionResult> Index() => View(_mapper.Map<ProfileViewModel>(await _current.GetAsync()));

        [HttpPost]
        public async Task<IActionResult> Index(ProfileViewModel model)
        {
            var tenant = await _current.GetAsync();
            if (model.ContactPreference == ContactPreference.Sms && string.IsNullOrWhiteSpace(model.MobilePhone))
                ModelState.AddModelError(nameof(model.MobilePhone), "Enter a mobile number to get texts from us.");
            if (model.ContactPreference == ContactPreference.Email && string.IsNullOrWhiteSpace(model.Email))
                ModelState.AddModelError(nameof(model.Email), "Enter an e-mail address to get e-mails from us.");
            if (!ModelState.IsValid)
            {
                var current = _mapper.Map<ProfileViewModel>(tenant);
                model.FullName = current.FullName;
                model.TenancyReference = current.TenancyReference;
                model.Address = current.Address;
                return View(model);
            }

            var before = new { tenant.Email, tenant.MobilePhone, tenant.ContactPreference, tenant.NeedsLargePrint };
            tenant.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
            tenant.MobilePhone = NormaliseMobile(model.MobilePhone);
            tenant.ContactPreference = model.ContactPreference;
            tenant.NeedsLargePrint = model.NeedsLargePrint;
            tenant.ModifiedAt = _clock.Now;
            tenant.ModifiedBy = User.Identity.Name;
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(User.Identity.Name, "Tenant", tenant.Id.ToString(), "ContactDetailsChanged", new { before, after = new { tenant.Email, tenant.MobilePhone, tenant.ContactPreference, tenant.NeedsLargePrint } });
            TempData["Message"] = "Your contact details have been updated.";
            return RedirectToAction(nameof(Index));
        }

        private static string NormaliseMobile(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var digits = value.Replace(" ", "").Replace("+44", "0");
            return digits.Length == 11 ? digits.Substring(0, 5) + " " + digits.Substring(5) : value.Trim();
        }
    }
}
