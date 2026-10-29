using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Tower.Models;
using Proseware.ControlTower.Tower.Services;

namespace Proseware.ControlTower.Tower.Controllers;

[ApiController]
[Route("api/shipments")]
public sealed class ShipmentsController : ControllerBase
{
    private readonly ShipmentQueries _queries;
    private readonly ControlTowerDbContext _db;

    public ShipmentsController(ShipmentQueries queries, ControlTowerDbContext db)
    {
        _queries = queries;
        _db = db;
    }

    [HttpGet]
    public Task<PagedResult<ShipmentRow>> Search(
        [FromQuery] string? q,
        [FromQuery] ShipmentStatus? status,
        [FromQuery] TransportMode? mode,
        [FromQuery] int? customerId,
        [FromQuery] bool openOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        _queries.SearchAsync(new ShipmentFilter(customerId, q, status, mode, openOnly, Page: page, PageSize: pageSize), ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ShipmentDetail>> Get(int id, CancellationToken ct)
    {
        var s = await _queries.GetAsync(id, ct);
        if (s is null) return NotFound();
        return await ToDetailAsync(s, ct);
    }

    [HttpGet("by-number/{number}")]
    public async Task<ActionResult<ShipmentDetail>> GetByNumber(string number, CancellationToken ct)
    {
        var s = await _queries.GetByNumberAsync(number.Trim().ToUpperInvariant(), ct);
        if (s is null) return NotFound();
        return await ToDetailAsync(s, ct);
    }

    private async Task<ShipmentDetail> ToDetailAsync(Shipment s, CancellationToken ct)
    {
        var alerts = await _db.Alerts.AsNoTracking().Where(a => a.ShipmentId == s.Id).ToListAsync(ct);
        return new ShipmentDetail(
            s.Id, s.Number, s.Customer?.Name ?? "", s.CustomerReference, s.Mode, s.IncotermCode, s.Status,
            s.Origin?.DisplayAddress ?? "", s.Destination?.DisplayAddress ?? "", s.ShipperName, s.ConsigneeName,
            s.PlannedPickup, s.PlannedDelivery, s.ActualPickup, s.ActualDelivery, s.Eta,
            s.GoodsDescription, s.GrossWeightKg, s.VolumeM3, s.PackageCount, s.IsDangerousGoods, s.TemperatureMin, s.TemperatureMax,
            s.Legs.OrderBy(l => l.Sequence).Select(l => new LegDto(
                l.Id, l.Sequence, l.Mode, l.Carrier?.Name, l.From?.Name ?? "", l.To?.Name ?? "", l.Status,
                l.PlannedDeparture, l.PlannedArrival, l.ActualDeparture, l.ActualArrival, l.Eta,
                l.CarrierReference, l.VesselOrFlight, l.Vehicle?.Plate, l.Carrier?.TrackingUrl(l.CarrierReference))).ToList(),
            s.Milestones.OrderByDescending(m => m.OccurredAt).Select(m => new MilestoneDto(
                m.Code, MilestoneCodes.Describe(m.Code), m.OccurredAt, m.LocationText, m.Source, m.Remarks)).ToList(),
            s.Packages.Select(p => new PackageDto(
                p.Sscc, p.Kind, p.WeightKg, $"{p.LengthCm}x{p.WidthCm}x{p.HeightCm} cm",
                p.DangerousGoods is { } dg ? $"UN{dg.UnNumber} {dg.ProperShippingName}, class {dg.HazardClass}" : null,
                p.ContainerNumber)).ToList(),
            s.CustomsDeclarations.Select(d => new CustomsDto(
                d.Id, d.Type.ToString(), d.Status.ToString(), d.Lrn, d.Mrn, d.SubmittedAt, d.ReleasedAt, d.RejectionReason)).ToList(),
            alerts.OrderByDescending(a => a.RaisedAt).Select(a => AlertService.ToRow(a, s.Number, s.Customer?.Name ?? "")).ToList());
    }
}
