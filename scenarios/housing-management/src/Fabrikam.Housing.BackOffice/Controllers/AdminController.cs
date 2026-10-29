using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.BackOffice.Infrastructure;
using Fabrikam.Housing.BackOffice.Models;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Communications;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Rent;
using Fabrikam.Housing.Repairs;
using Fabrikam.Housing.Tenancies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Fabrikam.Housing.BackOffice.Controllers
{
    [Authorize(Policies.Admin)]
    public class AdminController : Controller
    {
        private readonly HousingDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly AuditWriter _audit;
        private readonly IClock _clock;

        public AdminController(HousingDbContext db, IMemoryCache cache, AuditWriter audit, IClock clock)
        {
            _db = db;
            _cache = cache;
            _audit = audit;
            _clock = clock;
        }

        public IActionResult Index() => View();

        public async Task<IActionResult> BankHolidays()
        {
            return View(await _db.BankHolidays.AsNoTracking().OrderByDescending(b => b.Date).ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> AddBankHoliday(DateTime date, string name)
        {
            if (!await _db.BankHolidays.AnyAsync(b => b.Date == date.Date))
            {
                _db.BankHolidays.Add(new BankHoliday { Date = date.Date, Name = string.IsNullOrWhiteSpace(name) ? "Bank holiday" : name.Trim() });
                await _db.SaveChangesAsync();
                await _audit.WriteAsync(User.Login(), "BankHoliday", date.ToString("yyyy-MM-dd"), "Added", name);
            }
            // The working calendar is cached for a day in each application; clear ours now.
            _cache.Remove("bank-holidays");
            return RedirectToAction(nameof(BankHolidays));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteBankHoliday(DateTime date)
        {
            var day = await _db.BankHolidays.FindAsync(date.Date);
            if (day != null)
            {
                _db.BankHolidays.Remove(day);
                await _db.SaveChangesAsync();
                await _audit.WriteAsync(User.Login(), "BankHoliday", date.ToString("yyyy-MM-dd"), "Deleted");
            }
            _cache.Remove("bank-holidays");
            return RedirectToAction(nameof(BankHolidays));
        }

        public async Task<IActionResult> ScheduleOfRates()
        {
            return View(await _db.ScheduleOfRates.AsNoTracking().OrderBy(s => s.Code).ToListAsync());
        }

        public async Task<IActionResult> EditRate(int? id)
        {
            var item = id.HasValue ? await _db.ScheduleOfRates.FindAsync(id.Value) : new ScheduleOfRatesItem { Active = true, Unit = "IT", DefaultPriority = RepairPriority.Routine };
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost]
        public async Task<IActionResult> EditRate(int? id, ScheduleOfRatesItem input)
        {
            if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Description) || input.UnitPrice <= 0)
            {
                ModelState.AddModelError("", "Code, description and a unit price are required.");
                return View(input);
            }
            var item = id.HasValue ? await _db.ScheduleOfRates.FindAsync(id.Value) : null;
            if (item == null)
            {
                if (await _db.ScheduleOfRates.AnyAsync(s => s.Code == input.Code.Trim().ToUpper()))
                {
                    ModelState.AddModelError("", "That code already exists.");
                    return View(input);
                }
                item = new ScheduleOfRatesItem();
                _db.ScheduleOfRates.Add(item);
            }
            var oldPrice = item.UnitPrice;
            item.Code = input.Code.Trim().ToUpperInvariant();
            item.Description = input.Description.Trim();
            item.Trade = input.Trade;
            item.DefaultPriority = input.DefaultPriority;
            item.UnitPrice = input.UnitPrice;
            item.Unit = string.IsNullOrWhiteSpace(input.Unit) ? "IT" : input.Unit.Trim().ToUpperInvariant();
            item.Active = input.Active;
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(User.Login(), "ScheduleOfRates", item.Code, "Saved", new { oldPrice, item.UnitPrice });
            TempData["Message"] = $"{item.Code} saved.";
            return RedirectToAction(nameof(ScheduleOfRates));
        }

        public async Task<IActionResult> RentReview(decimal? cpi)
        {
            return View(await BuildReviewAsync(cpi ?? 0.5m));
        }

        /// <summary>
        /// Applies the annual rent review: new rents take effect from the first Monday in April and each tenancy
        /// gets a rent increase letter (statutory 28 days' notice), queued for the print run.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ApplyRentReview(decimal cpi, bool confirm)
        {
            var model = await BuildReviewAsync(cpi);
            if (!confirm)
            {
                TempData["Error"] = "Tick the confirmation box to apply the new rents.";
                return RedirectToAction(nameof(RentReview), new { cpi });
            }
            if (_clock.Today.AddDays(28) > model.EffectiveFrom)
            {
                TempData["Error"] = $"Tenants must have 28 days' notice: the review for {model.EffectiveFrom:dd/MM/yyyy} had to be applied by {model.EffectiveFrom.AddDays(-28):dd/MM/yyyy}.";
                return RedirectToAction(nameof(RentReview), new { cpi });
            }

            var newRents = model.Lines.ToDictionary(l => l.PropertyId, l => l.NewRent);
            var properties = await _db.Properties.Where(p => newRents.Keys.Contains(p.Id)).ToListAsync();
            var tenancies = await _db.Tenancies.Include(t => t.Tenants).Where(t => newRents.Keys.Contains(t.PropertyId) && t.Status != TenancyStatus.Ended).ToListAsync();
            var now = _clock.Now;
            foreach (var p in properties)
            {
                p.WeeklyNetRent = newRents[p.Id];
                p.ModifiedAt = now;
                p.ModifiedBy = User.Login();
            }
            foreach (var t in tenancies)
            {
                var p = properties.Single(x => x.Id == t.PropertyId);
                var oldCharge = t.WeeklyCharge;
                t.WeeklyCharge = p.WeeklyTotalCharge;
                var lead = t.Tenants.FirstOrDefault(x => x.IsLeadTenant);
                _db.Communications.Add(new CommunicationLog
                {
                    TenancyId = t.Id,
                    Channel = Channel.Letter,
                    TemplateCode = "RENTINC",
                    Recipient = lead?.FullName,
                    Subject = $"Your rent from {model.EffectiveFrom:d MMMM yyyy}",
                    Body = $"{{\"OldCharge\":\"{oldCharge:N2}\",\"NewCharge\":\"{t.WeeklyCharge:N2}\",\"EffectiveFrom\":\"{model.EffectiveFrom:yyyy-MM-dd}\"}}",
                    Status = CommunicationStatus.Queued,
                    CreatedAt = now
                });
            }
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(User.Login(), "RentReview", model.Year.ToString(), "Applied", new { cpi, properties = properties.Count, model.CurrentTotal, model.NewTotal });
            TempData["Message"] = $"New rents applied to {properties.Count} homes; {tenancies.Count} rent increase letters queued.";
            return RedirectToAction(nameof(RentReview), new { cpi });
        }

        private async Task<RentReviewViewModel> BuildReviewAsync(decimal cpi)
        {
            var today = _clock.Today;
            var effective = RentCalendar.RentYearStart(today >= RentCalendar.RentYearStart(today.Year) ? today.Year + 1 : today.Year);
            var review = new RentReview { CpiPercent = cpi };
            var properties = await _db.Properties.AsNoTracking().Where(p => p.Status != Assets.PropertyStatus.Disposed).OrderBy(p => p.Reference).ToListAsync();
            var lines = properties.Select(p =>
            {
                var newRent = review.NewWeeklyNetRent(p.WeeklyNetRent, p.Bedrooms);
                var uncapped = Math.Round(p.WeeklyNetRent * (1 + (cpi + review.ExtraPercent) / 100m), 2, MidpointRounding.AwayFromZero);
                return new RentReviewLine
                {
                    PropertyId = p.Id,
                    Reference = p.Reference,
                    Address = p.AddressLine1,
                    Bedrooms = p.Bedrooms,
                    CurrentRent = p.WeeklyNetRent,
                    NewRent = newRent,
                    Capped = newRent < uncapped
                };
            }).ToList();
            return new RentReviewViewModel
            {
                CpiPercent = cpi,
                Year = effective.Year,
                EffectiveFrom = effective,
                Lines = lines,
                CurrentTotal = lines.Sum(l => l.CurrentRent),
                NewTotal = lines.Sum(l => l.NewRent)
            };
        }
    }
}
