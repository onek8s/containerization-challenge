using Wingtip.FieldSales.Core;
using Wingtip.FieldSales.Data.Entities;

namespace Wingtip.FieldSales.Data
{
    public static class Mapping
    {
        public static Customer ToCore(this CustomerEntity e)
        {
            return new Customer
            {
                CustomerNo = e.CustomerNo, Name = e.Name, Street = e.Street, PostalCode = e.PostalCode, City = e.City,
                Email = e.Email, PriceGroup = e.PriceGroup, RepCode = e.RepCode, DeliveryDays = (DeliveryDays)e.DeliveryDays,
                CreditBlocked = e.CreditBlocked
            };
        }

        public static Product ToCore(this ProductEntity e)
        {
            return new Product
            {
                ItemNo = e.ItemNo, Name = e.Name, Category = e.Category, Unit = e.Unit, CaseSize = e.CaseSize,
                ListPrice = e.ListPrice, VatRate = e.VatRate, MinOrderQty = e.MinOrderQty, Active = e.Active
            };
        }

        public static PriceRule ToCore(this PriceRuleEntity e)
        {
            return new PriceRule { PriceGroup = e.PriceGroup, Category = e.Category, DiscountPercent = e.DiscountPercent };
        }

        public static Promotion ToCore(this PromotionEntity e)
        {
            return new Promotion { ItemNo = e.ItemNo, PromoPrice = e.PromoPrice, ValidFrom = e.ValidFrom, ValidTo = e.ValidTo };
        }
    }
}
