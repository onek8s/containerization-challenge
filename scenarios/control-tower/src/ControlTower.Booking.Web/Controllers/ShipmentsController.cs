using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Booking.Models;
using Proseware.ControlTower.Booking.Services;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Documents;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Customs;
using Proseware.ControlTower.Domain.Documents;
using Proseware.ControlTower.Domain.Pricing;

namespace Proseware.ControlTower.Booking.Controllers;

[Authorize(Policy = Policies.Staff)]
public class ShipmentsController : Controller
{
    private readonly ShipmentQueries _queries;
    private readonly ControlTowerDbContext _db;
    private readonly ShipmentCommands _commands;
    private readonly BookingService _booking;
    private readonly IDocumentStore _documents;
    private readonly IMapper _mapper;
    private readonly IConfiguration _configuration;

    public ShipmentsController(ShipmentQueries queries, ControlTowerDbContext db, ShipmentCommands commands, BookingService booking,
        IDocumentStore documents, IMapper mapper, IConfiguration configuration)
    {
        _queries = queries;
        _db = db;
        _commands = commands;
        _booking = booking;
        _documents = documents;
        _mapper = mapper;
        _configuration = configuration;
    }

    private string UserName => User.Identity?.Name ?? "unknown";

    public async Task<IActionResult> Index([FromQuery] ShipmentSearchViewModel model, CancellationToken ct)
    {
        model.Result = await _queries.SearchAsync(new ShipmentFilter(model.CustomerId, model.Q, model.Status, model.Mode, model.OpenOnly, Page: model.Page, PageSize: 50), ct);
        model.Customers = await CustomerListAsync(ct);
        return View(model);
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var shipment = await _queries.GetAsync(id, ct);
        if (shipment is null) return NotFound();
        var model = new ShipmentDetailsViewModel
        {
            Shipment = shipment,
            Legs = shipment.Legs.OrderBy(l => l.Sequence).Select(_mapper.Map<LegViewModel>).ToList(),
            Milestones = shipment.Milestones.OrderByDescending(m => m.OccurredAt).Select(_mapper.Map<MilestoneViewModel>).ToList(),
            Documents = await _db.Documents.AsNoTracking().Where(d => d.ShipmentId == id).OrderByDescending(d => d.CreatedAt).ToListAsync(ct),
            Alerts = await _db.Alerts.AsNoTracking().Where(a => a.ShipmentId == id).OrderByDescending(a => a.RaisedAt).Take(20).ToListAsync(ct),
            Printers = _configuration.GetSection("Labels:Printers").Get<string[]>() ?? Array.Empty<string>(),
            DeclarationErrors = shipment.CustomsDeclarations
                .Where(d => d.Status is DeclarationStatus.Draft or DeclarationStatus.Rejected)
                .ToDictionary(d => d.Id, d => DeclarationValidator.Validate(d)),
            ChargeableKg = ChargeableWeight.For(shipment.Mode, shipment.Packages)
        };
        ViewBag.Carriers = new SelectList(await _db.Carriers.AsNoTracking().Where(c => c.Active).OrderBy(c => c.Name).ToListAsync(ct), "Id", "Name");
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Milestone(int id, ManualMilestoneInput input, CancellationToken ct)
    {
        var tz = await _db.Shipments.Where(s => s.Id == id).Select(s => s.Origin!.TimeZoneId).FirstAsync(ct);
        await _commands.ReportMilestoneAsync(id, input.Code, LocalTime.FromLocal(input.OccurredAtLocal, tz), input.LocationCode, input.Remarks, UserName, ct);
        TempData["Message"] = $"{MilestoneCodes(input.Code)} sent; the timeline updates when it has been processed.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Booking)]
    public async Task<IActionResult> SubmitCustoms(int id, int declarationId, CancellationToken ct)
    {
        var errors = await _commands.SubmitCustomsAsync(id, declarationId, UserName, ct);
        TempData["Message"] = errors.Count == 0 ? "Declaration sent to customs." : "Declaration not sent: " + string.Join(" ", errors);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Booking)]
    public async Task<IActionResult> DeclarationLine(int id, int lineId, string commodityCode, string description, decimal statisticalValue, CancellationToken ct)
    {
        await _commands.UpdateDeclarationLineAsync(id, lineId, commodityCode, description, statisticalValue, ct);
        TempData["Message"] = "Goods item saved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDocument(int id, DocumentKind kind, CancellationToken ct)
    {
        await _commands.RequestDocumentAsync(id, kind, UserName, ct);
        TempData["Message"] = $"{kind} requested; it appears in the list within a minute.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Document(int id, Guid documentId, CancellationToken ct)
    {
        var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentId && d.ShipmentId == id, ct);
        if (doc is null) return NotFound();
        var stream = await _documents.OpenAsync(documentId, ct);
        return stream is null ? NotFound() : File(stream, doc.ContentType, doc.FileName);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PrintLabels(int id, string printer, int? packageId, CancellationToken ct)
    {
        await _commands.PrintLabelsAsync(id, packageId, printer, UserName, ct);
        TempData["Message"] = $"Labels sent to {printer}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Booking)]
    public async Task<IActionResult> BookLeg(int id, int legId, CancellationToken ct)
    {
        var error = await _commands.BookLegAsync(id, legId, UserName, ct);
        TempData["Message"] = error ?? "Booking sent to the carrier.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Booking)]
    public async Task<IActionResult> LegCarrier(int id, int legId, int? carrierId, string? reference, CancellationToken ct)
    {
        await _commands.SetLegCarrierAsync(id, legId, carrierId, reference, ct);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Booking)]
    public async Task<IActionResult> Confirm(int id, CancellationToken ct)
    {
        await _booking.ConfirmRequestAsync(id, UserName, ct);
        TempData["Message"] = "Booking request confirmed.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.Booking)]
    public async Task<IActionResult> Cancel(int id, string reason, CancellationToken ct)
    {
        var error = await _commands.CancelAsync(id, string.IsNullOrWhiteSpace(reason) ? "Cancelled" : reason, UserName, ct);
        TempData["Message"] = error ?? "Shipment cancelled.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private static string MilestoneCodes(string code) => Domain.Shipments.MilestoneCodes.Describe(code);

    private async Task<IEnumerable<SelectListItem>> CustomerListAsync(CancellationToken ct) =>
        (await _db.Customers.AsNoTracking().OrderBy(c => c.Name).Select(c => new { c.Id, c.Name }).ToListAsync(ct))
        .Select(c => new SelectListItem(c.Name, c.Id.ToString()));
}
