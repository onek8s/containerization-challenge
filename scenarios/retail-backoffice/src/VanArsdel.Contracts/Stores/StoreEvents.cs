using System;

namespace VanArsdel.Contracts.Stores
{
    public class StoreOpened
    {
        public string StoreCode { get; set; }
        public string Name { get; set; }
        public string LocationType { get; set; }
        public string Country { get; set; }
        public string City { get; set; }
        public string Currency { get; set; }
        public bool ShipFromStore { get; set; }
        public bool ClickAndCollect { get; set; }
        public DateTime OpenedOn { get; set; }
    }

    public class StoreUpdated
    {
        public string StoreCode { get; set; }
        public string Name { get; set; }
        public bool ShipFromStore { get; set; }
        public bool ClickAndCollect { get; set; }
    }

    public class StoreClosed
    {
        public string StoreCode { get; set; }
        public DateTime ClosedOn { get; set; }
    }

    public static class LocationTypes
    {
        public const string Store = "Store";
        public const string Outlet = "Outlet";
        public const string DistributionCentre = "DC";
    }
}
