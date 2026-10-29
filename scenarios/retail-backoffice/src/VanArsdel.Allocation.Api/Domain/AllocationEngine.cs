using System;
using System.Collections.Generic;
using System.Linq;
using VanArsdel.Contracts.Orders;
using VanArsdel.Contracts.Stores;

namespace VanArsdel.Allocation.Api.Domain
{
    public class StockAtLocation
    {
        public string LocationCode { get; set; }
        public string LocationType { get; set; }
        public string Country { get; set; }
        public bool ShipFromStore { get; set; }
        public bool ClickAndCollect { get; set; }
        public Dictionary<string, int> Available { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public int Get(string sku) => Available.TryGetValue(sku, out var q) ? q : 0;
    }

    public class AllocationRules
    {
        public int MaxShipments { get; set; } = 2;
        public bool AllowShipFromStore { get; set; } = true;
        public bool AllowCrossBorderStores { get; set; }

        /// <summary>A store must keep this many units after the pick, on top of its safety stock.</summary>
        public int StoreMinimumLeft { get; set; }
    }

    public class PlannedShipment
    {
        public string LocationCode { get; set; }
        public string LocationType { get; set; }
        public List<StockLineRef> Lines { get; set; } = new List<StockLineRef>();
    }

    public class AllocationPlan
    {
        public bool Success { get; set; }
        public string Strategy { get; set; }
        public string Reason { get; set; }
        public List<PlannedShipment> Shipments { get; set; } = new List<PlannedShipment>();
        public List<StockLineRef> ShortLines { get; set; } = new List<StockLineRef>();
    }

    /// <summary>
    /// Decides where an order ships from. Preference: one shipment from the DC, then one store that has
    /// everything (most stock first, same country), then a DC + store split, then any split up to the limit.
    /// </summary>
    public class AllocationEngine
    {
        public AllocationPlan Plan(IReadOnlyList<StockLineRef> lines, string deliveryMethod, string country, string collectStore,
            IReadOnlyList<StockAtLocation> stock, AllocationRules rules)
        {
            var demand = lines.GroupBy(l => l.Sku, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity), StringComparer.OrdinalIgnoreCase);
            var dc = stock.Where(s => s.LocationType == LocationTypes.DistributionCentre).OrderBy(s => s.LocationCode).ToList();
            var stores = stock.Where(s => s.LocationType != LocationTypes.DistributionCentre)
                .Where(s => rules.AllowCrossBorderStores || string.IsNullOrEmpty(country) || s.Country == country)
                .Select(s => Usable(s, rules))
                .ToList();

            if (deliveryMethod == DeliveryMethods.ClickAndCollect)
            {
                var store = stores.FirstOrDefault(s => string.Equals(s.LocationCode, collectStore, StringComparison.OrdinalIgnoreCase));
                if (store != null && Covers(store, demand))
                {
                    return Single(store, demand, "CollectFromStock");
                }
                var dcForCollect = dc.FirstOrDefault(d => Covers(d, demand));
                if (dcForCollect != null)
                {
                    return Single(dcForCollect, demand, "CollectViaDc");
                }
                return Fail(demand, dc.Concat(stores), "Not available for collection");
            }

            var fullDc = dc.FirstOrDefault(d => Covers(d, demand));
            if (fullDc != null) return Single(fullDc, demand, "Dc");

            var sfs = stores.Where(s => s.ShipFromStore && rules.AllowShipFromStore).ToList();
            var fullStore = sfs.Where(s => Covers(s, demand)).OrderByDescending(s => demand.Keys.Sum(k => s.Get(k))).ThenBy(s => s.LocationCode).FirstOrDefault();
            if (fullStore != null) return Single(fullStore, demand, "ShipFromStore");

            // Split: DC first, then the stores that cover most of what is left.
            var remaining = new Dictionary<string, int>(demand, StringComparer.OrdinalIgnoreCase);
            var plan = new AllocationPlan { Strategy = "Split" };
            foreach (var source in dc.Concat(sfs.OrderByDescending(s => remaining.Keys.Sum(k => Math.Min(s.Get(k), remaining[k])))))
            {
                if (plan.Shipments.Count >= rules.MaxShipments) break;
                var take = remaining.Where(r => r.Value > 0 && source.Get(r.Key) > 0)
                    .Select(r => new StockLineRef { Sku = r.Key, Quantity = Math.Min(r.Value, source.Get(r.Key)) })
                    .ToList();
                if (take.Count == 0) continue;
                plan.Shipments.Add(new PlannedShipment { LocationCode = source.LocationCode, LocationType = source.LocationType, Lines = take });
                foreach (var t in take) remaining[t.Sku] -= t.Quantity;
                if (remaining.Values.All(v => v == 0)) break;
            }

            if (remaining.Values.Any(v => v > 0))
            {
                return Fail(remaining.Where(r => r.Value > 0).ToDictionary(r => r.Key, r => r.Value), Array.Empty<StockAtLocation>(), "Not enough stock within the shipment limit");
            }
            plan.Success = true;
            return plan;
        }

        private static StockAtLocation Usable(StockAtLocation s, AllocationRules rules)
        {
            if (rules.StoreMinimumLeft <= 0) return s;
            return new StockAtLocation
            {
                LocationCode = s.LocationCode,
                LocationType = s.LocationType,
                Country = s.Country,
                ShipFromStore = s.ShipFromStore,
                ClickAndCollect = s.ClickAndCollect,
                Available = s.Available.ToDictionary(kv => kv.Key, kv => Math.Max(0, kv.Value - rules.StoreMinimumLeft), StringComparer.OrdinalIgnoreCase),
            };
        }

        private static bool Covers(StockAtLocation s, Dictionary<string, int> demand) => demand.All(d => s.Get(d.Key) >= d.Value);

        private static AllocationPlan Single(StockAtLocation s, Dictionary<string, int> demand, string strategy) => new AllocationPlan
        {
            Success = true,
            Strategy = strategy,
            Shipments =
            {
                new PlannedShipment
                {
                    LocationCode = s.LocationCode,
                    LocationType = s.LocationType,
                    Lines = demand.Select(d => new StockLineRef { Sku = d.Key, Quantity = d.Value }).ToList(),
                },
            },
        };

        private static AllocationPlan Fail(Dictionary<string, int> demand, IEnumerable<StockAtLocation> sources, string reason)
        {
            var all = sources.ToList();
            return new AllocationPlan
            {
                Success = false,
                Reason = reason,
                ShortLines = demand.Select(d => new StockLineRef
                {
                    Sku = d.Key,
                    Quantity = all.Count == 0 ? d.Value : Math.Max(0, d.Value - all.Max(s => s.Get(d.Key))),
                }).Where(l => l.Quantity > 0).ToList(),
            };
        }
    }
}
