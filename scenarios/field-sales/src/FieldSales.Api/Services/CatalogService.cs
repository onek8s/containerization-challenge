using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Wingtip.FieldSales.Api.Infrastructure;
using Wingtip.FieldSales.Contracts;
using Wingtip.FieldSales.Core;
using Wingtip.FieldSales.Data;

namespace Wingtip.FieldSales.Api.Services
{
    public class CatalogService
    {
        private readonly FieldSalesDbContext _db;
        private readonly SalesProcedures _procedures;
        private readonly ApiSettings _settings;

        public CatalogService(FieldSalesDbContext db, SalesProcedures procedures, ApiSettings settings)
        {
            _db = db;
            _procedures = procedures;
            _settings = settings;
        }

        public async Task<CatalogResponse> GetChangesAsync(long sinceVersion)
        {
            // Read the version first: rows changed while the query runs are sent again on the next sync.
            var version = await _procedures.GetCatalogVersionAsync();
            var rows = await _procedures.GetCatalogChangesAsync(sinceVersion);

            var rules = await _db.PriceRules.AsNoTracking().ToListAsync();
            var today = DateTime.Today;
            var promotions = await _db.Promotions.AsNoTracking().Where(p => p.ValidTo >= today).ToListAsync();
            var holidays = await _db.Holidays.AsNoTracking().Where(h => h.Day >= today).Select(h => h.Day).ToListAsync();

            return new CatalogResponse
            {
                Version = version,
                PriceListVersion = await _procedures.GetPriceListVersionAsync(),
                FullRefresh = sinceVersion == 0,
                Products = rows.Select(r => new ProductDto
                {
                    ItemNo = r.ItemNo, Name = r.Name, Category = r.Category, Unit = r.Unit, CaseSize = r.CaseSize,
                    ListPrice = r.ListPrice, VatRate = r.VatRate, MinOrderQty = r.MinOrderQty, Active = r.Active
                }).ToList(),
                PriceRules = rules.Select(r => new PriceRuleDto { PriceGroup = r.PriceGroup, Category = r.Category, DiscountPercent = r.DiscountPercent }).ToList(),
                Promotions = promotions.Select(p => new PromotionDto { ItemNo = p.ItemNo, PromoPrice = p.PromoPrice, ValidFrom = p.ValidFrom, ValidTo = p.ValidTo }).ToList(),
                Holidays = holidays,
                CutOff = _settings.OrderCutOff.ToString(@"hh\:mm", CultureInfo.InvariantCulture)
            };
        }

        public async Task<PriceSnapshot> LoadPricingAsync()
        {
            var products = await _db.Products.AsNoTracking().ToListAsync();
            var rules = await _db.PriceRules.AsNoTracking().ToListAsync();
            var promotions = await _db.Promotions.AsNoTracking().ToListAsync();
            var holidays = await _db.Holidays.AsNoTracking().Select(h => h.Day).ToListAsync();

            return new PriceSnapshot
            {
                Products = products.Select(p => p.ToCore()).ToDictionary(p => p.ItemNo, StringComparer.OrdinalIgnoreCase),
                Calculator = new PriceCalculator(rules.Select(r => r.ToCore()), promotions.Select(p => p.ToCore())),
                Calendar = new DeliveryCalendar(_settings.OrderCutOff, holidays)
            };
        }
    }

    public class PriceSnapshot
    {
        public IDictionary<string, Product> Products { get; set; }
        public PriceCalculator Calculator { get; set; }
        public DeliveryCalendar Calendar { get; set; }
    }
}
