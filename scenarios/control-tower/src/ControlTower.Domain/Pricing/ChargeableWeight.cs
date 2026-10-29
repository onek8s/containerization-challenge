using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Domain.Pricing;

public static class ChargeableWeight
{
    /// <summary>IATA volumetric factor: 1 m³ = 166.67 kg (6000 cm³/kg).</summary>
    public const decimal AirKgPerM3 = 1_000_000m / 6000m;
    /// <summary>Nordic road freight: 1 m³ = 333 kg, 1 loading metre = 1850 kg.</summary>
    public const decimal RoadKgPerM3 = 333m;
    public const decimal RoadKgPerLdm = 1850m;
    /// <summary>LCL sea freight: weight or measure, 1 m³ = 1000 kg.</summary>
    public const decimal SeaKgPerM3 = 1000m;

    public static decimal For(TransportMode mode, IEnumerable<Package> packages)
    {
        var list = packages.ToList();
        var gross = list.Sum(p => p.WeightKg * p.Quantity);
        var volume = list.Sum(p => p.VolumeM3 * p.Quantity);
        var ldm = list.Sum(p => p.LoadingMeters);
        var chargeable = mode switch
        {
            TransportMode.Air => Math.Max(gross, volume * AirKgPerM3),
            TransportMode.Road or TransportMode.Rail => Math.Max(gross, Math.Max(volume * RoadKgPerM3, ldm * RoadKgPerLdm)),
            TransportMode.Sea => Math.Max(gross, volume * SeaKgPerM3),
            _ => gross
        };
        // Air rounds up to the next half kilo, others to the next full kilo.
        return mode == TransportMode.Air
            ? Math.Ceiling(chargeable * 2) / 2
            : Math.Ceiling(chargeable);
    }
}
