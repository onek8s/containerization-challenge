namespace Proseware.ControlTower.Domain;

public enum TransportMode
{
    Road = 1,
    Sea = 2,
    Air = 3,
    Rail = 4
}

public enum ShipmentStatus
{
    Requested = 0,
    Booked = 10,
    PickedUp = 20,
    InTransit = 30,
    AtTerminal = 40,
    CustomsHold = 50,
    OutForDelivery = 60,
    Delivered = 70,
    Cancelled = 90
}

public enum LegStatus
{
    Planned = 0,
    Confirmed = 1,
    Departed = 2,
    Arrived = 3,
    Cancelled = 9
}

public enum PackageKind
{
    Parcel = 1,
    Pallet = 2,
    HalfPallet = 3,
    Crate = 4,
    Container20 = 20,
    Container40 = 40,
    Container40HighCube = 45
}

public enum LocationKind
{
    Terminal = 1,
    Seaport = 2,
    Airport = 3,
    CustomerSite = 4,
    CarrierDepot = 5,
    BorderCrossing = 6
}

public enum Severity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

public enum CarrierIntegration
{
    Manual = 0,
    EdifactSftp = 1,
    RestApi = 2
}
