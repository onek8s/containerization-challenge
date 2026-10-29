using AutoMapper;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Booking.Models;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain.Pricing;

namespace Proseware.ControlTower.Booking.Controllers;

[Authorize(Policy = Policies.Admin)]
public class RateCardsController : Controller
{
    private readonly ControlTowerDbContext _db;
    private readonly IMapper _mapper;
    private readonly IValidator<RateCardEditModel> _validator;

    public RateCardsController(ControlTowerDbContext db, IMapper mapper, IValidator<RateCardEditModel> validator)
    {
        _db = db;
        _mapper = mapper;
        _validator = validator;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Carriers = await _db.Carriers.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        return View(await _db.RateCards.AsNoTracking().Include(r => r.Breaks)
            .OrderBy(r => r.Mode).ThenBy(r => r.OriginCountry).ThenBy(r => r.DestinationCountry).ToListAsync(ct));
    }

    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        RateCardEditModel model;
        if (id is null)
        {
            model = new RateCardEditModel { ValidFrom = DateTime.Today, ValidTo = new DateTime(DateTime.Today.Year, 12, 31), Breaks = "0; 0.00" };
        }
        else
        {
            var card = await _db.RateCards.AsNoTracking().Include(r => r.Breaks).FirstOrDefaultAsync(r => r.Id == id, ct);
            if (card is null) return NotFound();
            model = _mapper.Map<RateCardEditModel>(card);
        }
        model.Carriers = await CarriersAsync(ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(RateCardEditModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        foreach (var e in result.Errors) ModelState.AddModelError(e.PropertyName, e.ErrorMessage);
        if (!ModelState.IsValid)
        {
            model.Carriers = await CarriersAsync(ct);
            return View(model);
        }

        RateCard card;
        if (model.Id == 0)
        {
            card = new RateCard();
            _db.RateCards.Add(card);
        }
        else
        {
            card = await _db.RateCards.Include(r => r.Breaks).FirstAsync(r => r.Id == model.Id, ct);
            _db.RemoveRange(card.Breaks);
            card.Breaks.Clear();
        }
        card.CarrierId = model.CarrierId;
        card.Mode = model.Mode;
        card.OriginCountry = model.OriginCountry.ToUpperInvariant();
        card.DestinationCountry = model.DestinationCountry.ToUpperInvariant();
        card.OriginPostalPrefix = string.IsNullOrWhiteSpace(model.OriginPostalPrefix) ? null : model.OriginPostalPrefix.Trim();
        card.Currency = model.Currency.ToUpperInvariant();
        card.MinimumCharge = model.MinimumCharge;
        card.FuelSurchargePercent = model.FuelSurchargePercent;
        card.TransitDays = model.TransitDays;
        card.ValidFrom = model.ValidFrom.Date;
        card.ValidTo = model.ValidTo.Date;
        RateBreakParser.TryParse(model.Breaks, out var breaks);
        foreach (var (from, price) in breaks)
            card.Breaks.Add(new RateBreak { FromKg = from, PricePer100Kg = price });
        await _db.SaveChangesAsync(ct);
        TempData["Message"] = $"Rate card {card.OriginCountry}-{card.DestinationCountry} {card.Mode} saved.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IEnumerable<SelectListItem>> CarriersAsync(CancellationToken ct) =>
        (await _db.Carriers.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct)).Select(c => new SelectListItem(c.Name, c.Id.ToString()));
}
