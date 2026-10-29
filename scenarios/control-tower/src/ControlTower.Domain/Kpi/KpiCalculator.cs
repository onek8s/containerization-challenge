using Proseware.ControlTower.Domain.Pricing;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Domain.Tracking;

namespace Proseware.ControlTower.Domain.Kpi;

public sealed class KpiCalculator
{
    /// <summary>Delivery counts as on time up to the end of the planned delivery day plus a grace period.</summary>
    public TimeSpan DeliveryGrace { get; init; } = TimeSpan.FromHours(2);
    public TimeSpan PickupGrace { get; init; } = TimeSpan.FromHours(1);

    public IReadOnlyList<DailyKpi> Calculate(DateTime day, IReadOnlyCollection<Shipment> shipments, IReadOnlyCollection<ShipmentAlert> alerts, DateTimeOffset now)
    {
        var dayStart = new DateTimeOffset(day.Date, TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        var groups = shipments
            .GroupBy(s => (s.CustomerId, s.Mode))
            .Select(g =>
            {
                var booked = g.Count(s => s.CreatedAt >= dayStart && s.CreatedAt < dayEnd);
                var delivered = g.Where(s => s.ActualDelivery >= dayStart && s.ActualDelivery < dayEnd).ToList();
                var pickupsDue = g.Where(s => s.PlannedPickup >= dayStart && s.PlannedPickup < dayEnd && s.Status != ShipmentStatus.Cancelled).ToList();
                var ids = g.Select(s => s.Id).ToHashSet();
                var dayAlerts = alerts.Where(a => ids.Contains(a.ShipmentId) && a.RaisedAt >= dayStart && a.RaisedAt < dayEnd).ToList();
                return new DailyKpi
                {
                    Day = day.Date,
                    CustomerId = g.Key.CustomerId,
                    Mode = g.Key.Mode,
                    ShipmentsBooked = booked,
                    ShipmentsDelivered = delivered.Count,
                    DeliveredOnTime = delivered.Count(IsDeliveredOnTime),
                    PickupsDue = pickupsDue.Count,
                    PickedUpOnTime = pickupsDue.Count(s => s.ActualPickup is { } p && p <= s.PlannedPickup + PickupGrace),
                    AlertsRaised = dayAlerts.Count,
                    CriticalAlerts = dayAlerts.Count(a => a.Severity == Severity.Critical),
                    AverageTransitHours = delivered.Where(s => s.ActualPickup.HasValue)
                        .Select(s => (s.ActualDelivery!.Value - s.ActualPickup!.Value).TotalHours)
                        .DefaultIfEmpty(0).Average(),
                    Revenue = delivered.SelectMany(s => s.Charges).Where(c => c.Currency == "EUR").Sum(c => c.Amount),
                    ChargeableKg = delivered.Sum(s => ChargeableWeight.For(s.Mode, s.Packages)),
                    CalculatedAt = now
                };
            })
            .Where(k => k.ShipmentsBooked + k.ShipmentsDelivered + k.PickupsDue + k.AlertsRaised > 0)
            .ToList();
        return groups;
    }

    public bool IsDeliveredOnTime(Shipment s)
    {
        if (s.ActualDelivery is not { } delivered) return false;
        var tz = s.Destination?.TimeZoneId;
        var deadline = s.PlannedDelivery;
        if (tz is not null)
        {
            var local = LocalTime.ToLocal(s.PlannedDelivery, tz);
            deadline = LocalTime.FromLocal(local.Date.AddDays(1).AddTicks(-1), tz);
        }
        return delivered <= deadline + DeliveryGrace;
    }

    public static DailyKpi Sum(IEnumerable<DailyKpi> rows)
    {
        var list = rows.ToList();
        var delivered = list.Sum(r => r.ShipmentsDelivered);
        return new DailyKpi
        {
            Day = list.Count > 0 ? list.Min(r => r.Day) : DateTime.MinValue,
            ShipmentsBooked = list.Sum(r => r.ShipmentsBooked),
            ShipmentsDelivered = delivered,
            DeliveredOnTime = list.Sum(r => r.DeliveredOnTime),
            PickupsDue = list.Sum(r => r.PickupsDue),
            PickedUpOnTime = list.Sum(r => r.PickedUpOnTime),
            AlertsRaised = list.Sum(r => r.AlertsRaised),
            CriticalAlerts = list.Sum(r => r.CriticalAlerts),
            AverageTransitHours = delivered == 0 ? 0 : list.Sum(r => r.AverageTransitHours * r.ShipmentsDelivered) / delivered,
            Revenue = list.Sum(r => r.Revenue),
            ChargeableKg = list.Sum(r => r.ChargeableKg)
        };
    }
}
