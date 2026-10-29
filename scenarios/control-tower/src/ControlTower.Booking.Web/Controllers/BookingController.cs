using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Booking.Models;
using Proseware.ControlTower.Booking.Services;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;

namespace Proseware.ControlTower.Booking.Controllers;

/// <summary>Three-step booking wizard: parties and route, packages, quote and confirmation.</summary>
[Authorize(Policy = Policies.Booking)]
public class BookingController : Controller
{
    private readonly ControlTowerDbContext _db;
    private readonly BookingDraftStore _drafts;
    private readonly QuoteService _quotes;
    private readonly BookingService _booking;
    private readonly BookingPartiesValidator _partiesValidator;
    private readonly BookingPackagesValidator _packagesValidator;

    public BookingController(ControlTowerDbContext db, BookingDraftStore drafts, QuoteService quotes, BookingService booking,
        BookingPartiesValidator partiesValidator, BookingPackagesValidator packagesValidator)
    {
        _db = db;
        _drafts = drafts;
        _quotes = quotes;
        _booking = booking;
        _partiesValidator = partiesValidator;
        _packagesValidator = packagesValidator;
    }

    [HttpGet]
    public IActionResult New()
    {
        _drafts.Clear();
        return RedirectToAction(nameof(Parties));
    }

    [HttpGet]
    public async Task<IActionResult> Parties(CancellationToken ct) => View(await ModelAsync(_drafts.Get(), ct));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Parties(BookingDraft input, CancellationToken ct)
    {
        var draft = _drafts.Get();
        draft.CustomerId = input.CustomerId;
        draft.CustomerReference = input.CustomerReference;
        draft.Mode = input.Mode;
        draft.IncotermCode = input.IncotermCode;
        draft.OriginId = input.OriginId;
        draft.DestinationId = input.DestinationId;
        draft.ShipperName = input.ShipperName;
        draft.ConsigneeName = input.ConsigneeName;
        draft.ConsigneeEmail = input.ConsigneeEmail;
        draft.NotifyPhone = input.NotifyPhone;
        draft.PickupDate = input.PickupDate;
        draft.PickupTime = input.PickupTime;
        draft.GoodsDescription = input.GoodsDescription;
        draft.DeclaredValue = input.DeclaredValue;
        draft.TemperatureMin = input.TemperatureMin;
        draft.TemperatureMax = input.TemperatureMax;
        draft.CustomsClearance = input.CustomsClearance;
        draft.Insurance = input.Insurance;

        var result = await _partiesValidator.ValidateAsync(draft, ct);
        if (!result.IsValid)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("Draft." + e.PropertyName, e.ErrorMessage);
            return View(await ModelAsync(draft, ct));
        }
        draft.Step = Math.Max(draft.Step, 2);
        _drafts.Save(draft);
        return RedirectToAction(nameof(Packages));
    }

    [HttpGet]
    public async Task<IActionResult> Packages(CancellationToken ct)
    {
        var draft = _drafts.Get();
        if (draft.Step < 2) return RedirectToAction(nameof(Parties));
        if (draft.Packages.Count == 0) draft.Packages.Add(new PackageLine());
        return View(await ModelAsync(draft, ct));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Packages(List<PackageLine> packages, string? command, CancellationToken ct)
    {
        var draft = _drafts.Get();
        draft.Packages = packages ?? new List<PackageLine>();
        if (command == "add")
        {
            draft.Packages.Add(new PackageLine { Kind = draft.Packages.LastOrDefault()?.Kind ?? PackageKind.Pallet });
            _drafts.Save(draft);
            ModelState.Clear();
            return View(await ModelAsync(draft, ct));
        }
        if (command?.StartsWith("remove:", StringComparison.Ordinal) == true && int.TryParse(command[7..], out var index) && index < draft.Packages.Count)
        {
            draft.Packages.RemoveAt(index);
            _drafts.Save(draft);
            ModelState.Clear();
            return View(await ModelAsync(draft, ct));
        }

        var result = await _packagesValidator.ValidateAsync(draft, ct);
        if (!result.IsValid)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(e.PropertyName, e.ErrorMessage);
            _drafts.Save(draft);
            return View(await ModelAsync(draft, ct));
        }
        draft.Step = 3;
        _drafts.Save(draft);
        return RedirectToAction(nameof(Review));
    }

    [HttpGet]
    public async Task<IActionResult> Review(CancellationToken ct)
    {
        var draft = _drafts.Get();
        if (draft.Step < 3) return RedirectToAction(nameof(Packages));
        var model = await ModelAsync(draft, ct);
        model.Quotes = await _quotes.QuoteAsync(draft, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int? rateCardId, CancellationToken ct)
    {
        var draft = _drafts.Get();
        if (draft.Step < 3) return RedirectToAction(nameof(Packages));
        var parties = await _partiesValidator.ValidateAsync(draft, ct);
        var packages = await _packagesValidator.ValidateAsync(draft, ct);
        if (!parties.IsValid || !packages.IsValid)
        {
            TempData["Message"] = "The booking is incomplete: " + string.Join(" ", parties.Errors.Concat(packages.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Parties));
        }
        draft.SelectedRateCardId = rateCardId;
        var shipment = await _booking.CreateAsync(draft, User.Identity?.Name ?? "unknown", ct);
        _drafts.Clear();
        TempData["Message"] = $"Shipment {shipment.Number} booked.";
        return RedirectToAction("Details", "Shipments", new { id = shipment.Id });
    }

    /// <summary>Live quote for the packages step (called from booking.js).</summary>
    [HttpPost("/api/quote"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Quote([FromBody] List<PackageLine> packages, CancellationToken ct)
    {
        var draft = _drafts.Get();
        draft.Packages = packages;
        var quotes = await _quotes.QuoteAsync(draft, ct);
        return Json(quotes.Select(q => new { q.Carrier, q.TransitDays, q.ChargeableKg, q.Total, q.Currency }));
    }

    private async Task<BookingStepViewModel> ModelAsync(BookingDraft draft, CancellationToken ct)
    {
        var locations = await _db.Locations.AsNoTracking().OrderBy(l => l.CountryCode).ThenBy(l => l.City).ThenBy(l => l.Name)
            .Select(l => new { l.Id, l.Code, l.Name, l.City, l.CountryCode }).ToListAsync(ct);
        var customers = await _db.Customers.AsNoTracking().OrderBy(c => c.Name).Select(c => new { c.Id, c.Name, c.Code }).ToListAsync(ct);
        return new BookingStepViewModel
        {
            Draft = draft,
            Customers = customers.Select(c => new SelectListItem($"{c.Name} ({c.Code})", c.Id.ToString())),
            Locations = locations.Select(l => new SelectListItem($"{l.CountryCode} {l.City} - {l.Name} ({l.Code})", l.Id.ToString())),
            CustomerName = customers.FirstOrDefault(c => c.Id == draft.CustomerId)?.Name,
            OriginText = locations.FirstOrDefault(l => l.Id == draft.OriginId) is { } o ? $"{o.Name}, {o.City} {o.CountryCode}" : null,
            DestinationText = locations.FirstOrDefault(l => l.Id == draft.DestinationId) is { } d ? $"{d.Name}, {d.City} {d.CountryCode}" : null
        };
    }
}
