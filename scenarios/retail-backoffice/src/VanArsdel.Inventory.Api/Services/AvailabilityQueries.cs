using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VanArsdel.Contracts.Stores;
using VanArsdel.Inventory.Api.Data;
using VanArsdel.Inventory.Api.Domain;

namespace VanArsdel.Inventory.Api.Services
{
    public class LocationAvailability
    {
        public Location Location { get; set; }
        public List<StockLevel> Levels { get; set; } = new List<StockLevel>();
    }

    public class StyleAvailabilitySummary
    {
        public string StyleCode { get; set; }
        public List<SkuAvailabilitySummary> Skus { get; set; } = new List<SkuAvailabilitySummary>();
    }

    public class SkuAvailabilitySummary
    {
        public string Sku { get; set; }
        public bool OnlineAvailable { get; set; }
        public int StoresWithStock { get; set; }
        public string Level { get; set; }
    }

    public class AvailabilityQueries
    {
        private readonly InventoryDbContext _db;

        public AvailabilityQueries(InventoryDbContext db)
        {
            _db = db;
        }

        public async Task<List<LocationAvailability>> GetAsync(IReadOnlyCollection<string> skus, IReadOnlyCollection<string> locationCodes,
            string country, bool shipFromStoreOnly, CancellationToken ct)
        {
            var upper = skus.Select(s => s.ToUpperInvariant()).Distinct().ToList();
            var locations = _db.Locations.AsNoTracking().Where(l => l.Active);
            if (locationCodes.Count > 0) locations = locations.Where(l => locationCodes.Contains(l.Code));
            if (!string.IsNullOrEmpty(country)) locations = locations.Where(l => l.Country == country || l.Type == LocationTypes.DistributionCentre);
            if (shipFromStoreOnly) locations = locations.Where(l => l.ShipFromStore || l.Type == LocationTypes.DistributionCentre);
            var locationList = await locations.ToListAsync(ct);
            var codes = locationList.Select(l => l.Code).ToList();

            var levels = await _db.StockLevels.AsNoTracking()
                .Where(s => upper.Contains(s.Sku) && codes.Contains(s.LocationCode))
                .ToListAsync(ct);
            var byLocation = levels.ToLookup(l => l.LocationCode);

            return locationList.Select(l => new LocationAvailability { Location = l, Levels = byLocation[l.Code].ToList() }).ToList();
        }

        /// <summary>Coarse availability for the web shop product page (no exact quantities).</summary>
        public async Task<StyleAvailabilitySummary> StyleSummaryAsync(string styleCode, CancellationToken ct)
        {
            var skus = await _db.Items.AsNoTracking().Where(i => i.StyleCode == styleCode).Select(i => i.Sku).ToListAsync(ct);
            var levels = await (from s in _db.StockLevels.AsNoTracking()
                                join l in _db.Locations.AsNoTracking() on s.LocationCode equals l.Code
                                where skus.Contains(s.Sku) && l.Active
                                select new { s.Sku, s.OnHand, s.Reserved, l.Type, l.SafetyStock }).ToListAsync(ct);

            var summary = new StyleAvailabilitySummary { StyleCode = styleCode };
            foreach (var sku in skus.OrderBy(s => s))
            {
                var rows = levels.Where(r => r.Sku == sku).ToList();
                var dc = rows.Where(r => r.Type == LocationTypes.DistributionCentre).Sum(r => Math.Max(0, r.OnHand - r.Reserved));
                var stores = rows.Count(r => r.Type != LocationTypes.DistributionCentre && r.OnHand - r.Reserved > r.SafetyStock);
                summary.Skus.Add(new SkuAvailabilitySummary
                {
                    Sku = sku,
                    OnlineAvailable = dc > 0 || stores > 0,
                    StoresWithStock = stores,
                    Level = dc > 10 ? "InStock" : dc > 0 ? "Low" : stores > 0 ? "StoresOnly" : "OutOfStock",
                });
            }
            return summary;
        }
    }
}
