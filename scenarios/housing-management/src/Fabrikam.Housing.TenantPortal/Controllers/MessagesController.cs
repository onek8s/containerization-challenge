using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Tenancies;
using Fabrikam.Housing.TenantPortal.Models;
using Fabrikam.Housing.TenantPortal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fabrikam.Housing.TenantPortal.Controllers
{
    public class MessagesController : Controller
    {
        private readonly CurrentTenant _current;
        private readonly HousingDbContext _db;
        private readonly IMapper _mapper;
        private readonly IClock _clock;

        public MessagesController(CurrentTenant current, HousingDbContext db, IMapper mapper, IClock clock)
        {
            _current = current;
            _db = db;
            _mapper = mapper;
            _clock = clock;
        }

        public async Task<IActionResult> Index()
        {
            var tenancy = await _current.TenancyAsync();
            var items = await _db.Enquiries.AsNoTracking().Where(e => e.TenancyId == tenancy.Id)
                .OrderByDescending(e => e.CreatedAt).ToListAsync();
            return View(_mapper.Map<List<EnquiryViewModel>>(items));
        }

        [HttpGet]
        public IActionResult New(EnquiryTopic? topic) => View(new NewEnquiryViewModel { Topic = topic });

        [HttpPost]
        public async Task<IActionResult> New(NewEnquiryViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var tenant = await _current.GetAsync();
            _db.Enquiries.Add(new TenantEnquiry
            {
                TenancyId = tenant.TenancyId,
                Topic = model.Topic.Value,
                Subject = model.Subject.Trim(),
                Message = model.Message.Trim(),
                CreatedAt = _clock.Now,
                CreatedBy = tenant.FullName,
                // Rent queries go to the income team, everything else to the patch housing officer.
                AssignedTo = model.Topic == EnquiryTopic.Rent ? "INCOME" : tenant.Tenancy.Property.PatchCode
            });
            await _db.SaveChangesAsync();
            TempData["Message"] = model.Topic == EnquiryTopic.Complaint
                ? "Thank you. We have logged your complaint and will reply within 10 working days."
                : "Thank you. We will reply within 5 working days.";
            return RedirectToAction(nameof(Index));
        }
    }
}
