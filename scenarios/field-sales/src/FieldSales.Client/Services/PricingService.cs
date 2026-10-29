using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Wingtip.FieldSales.Contracts;
using Wingtip.FieldSales.Core;

namespace Wingtip.FieldSales.Client.Services
{
    /// <summary>Builds the shared pricing and delivery rules from the cached catalog so that prices are known offline.</summary>
    public class PricingService
    {
        public PricingService(CatalogCache catalog)
        {
            Catalog = catalog;
            Products = catalog.Products.Values.Select(ToCore).ToDictionary(p => p.ItemNo, StringComparer.OrdinalIgnoreCase);
            Calculator = new PriceCalculator(
                catalog.PriceRules.Select(r => new PriceRule { PriceGroup = r.PriceGroup, Category = r.Category, DiscountPercent = r.DiscountPercent }),
                catalog.Promotions.Select(p => new Promotion { ItemNo = p.ItemNo, PromoPrice = p.PromoPrice, ValidFrom = p.ValidFrom, ValidTo = p.ValidTo }));
            Calendar = new DeliveryCalendar(TimeSpan.Parse(catalog.CutOff ?? "14:00", CultureInfo.InvariantCulture), catalog.Holidays);
        }

        public CatalogCache Catalog { get; private set; }
        public IDictionary<string, Product> Products { get; private set; }
        public PriceCalculator Calculator { get; private set; }
        public DeliveryCalendar Calendar { get; private set; }

        public static Customer ToCore(CustomerDto c)
        {
            return new Customer
            {
                CustomerNo = c.CustomerNo, Name = c.Name, Street = c.Street, PostalCode = c.PostalCode, City = c.City, Email = c.Email,
                PriceGroup = c.PriceGroup, RepCode = c.RepCode, DeliveryDays = (DeliveryDays)c.DeliveryDays, CreditBlocked = c.CreditBlocked
            };
        }

        private static Product ToCore(ProductDto p)
        {
            return new Product
            {
                ItemNo = p.ItemNo, Name = p.Name, Category = p.Category, Unit = p.Unit, CaseSize = p.CaseSize, ListPrice = p.ListPrice,
                VatRate = p.VatRate, MinOrderQty = p.MinOrderQty, Active = p.Active
            };
        }
    }
}
