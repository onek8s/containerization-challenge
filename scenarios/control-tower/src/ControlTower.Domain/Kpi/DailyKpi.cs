namespace Proseware.ControlTower.Domain.Kpi;

public class DailyKpi
{
    public int Id { get; set; }
    public DateTime Day { get; set; }
    public int? CustomerId { get; set; }
    public int? CarrierId { get; set; }
    public TransportMode Mode { get; set; }
    public int ShipmentsBooked { get; set; }
    public int ShipmentsDelivered { get; set; }
    public int DeliveredOnTime { get; set; }
    public int PickedUpOnTime { get; set; }
    public int PickupsDue { get; set; }
    public int AlertsRaised { get; set; }
    public int CriticalAlerts { get; set; }
    public double AverageTransitHours { get; set; }
    public decimal Revenue { get; set; }
    public decimal ChargeableKg { get; set; }
    public DateTimeOffset CalculatedAt { get; set; }

    public double OnTimeDeliveryPercent => ShipmentsDelivered == 0 ? 0 : Math.Round(100.0 * DeliveredOnTime / ShipmentsDelivered, 1);

    public double OnTimePickupPercent => PickupsDue == 0 ? 0 : Math.Round(100.0 * PickedUpOnTime / PickupsDue, 1);
}
