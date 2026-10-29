using System.Linq;
using System.Threading.Tasks;
using VanArsdel.Catalog.Api.Domain;

namespace VanArsdel.Catalog.Api.Data
{
    /// <summary>Brands and the category tree. Products are loaded through the API (LabStubs seed).</summary>
    public static class CatalogSeed
    {
        public static async Task SeedAsync(CatalogDbContext db)
        {
            if (db.Brands.Any()) return;

            db.Brands.AddRange(
                new Brand { Code = "VA", Name = "VanArsdel", OwnBrand = true },
                new Brand { Code = "VAS", Name = "VanArsdel Studio", OwnBrand = true },
                new Brand { Code = "NRD", Name = "Nordwind Outdoor", OwnBrand = false },
                new Brand { Code = "LMN", Name = "Lumen Denim", OwnBrand = false },
                new Brand { Code = "KOT", Name = "Koti Basics", OwnBrand = true });
            await db.SaveChangesAsync();

            var roots = new[]
            {
                new Category { Code = "WOMEN", Name = "Women" },
                new Category { Code = "MEN", Name = "Men" },
                new Category { Code = "KIDS", Name = "Kids" },
                new Category { Code = "ACC", Name = "Accessories" },
            };
            db.Categories.AddRange(roots);
            await db.SaveChangesAsync();

            int Root(string code) => roots.Single(r => r.Code == code).Id;
            db.Categories.AddRange(
                new Category { Code = "W-DRESS", Name = "Dresses", ParentId = Root("WOMEN"), ErpItemGroup = "APP-W", SizeScale = "ALPHA" },
                new Category { Code = "W-KNIT", Name = "Knitwear", ParentId = Root("WOMEN"), ErpItemGroup = "APP-W", SizeScale = "ALPHA" },
                new Category { Code = "W-JEANS", Name = "Jeans", ParentId = Root("WOMEN"), ErpItemGroup = "DNM-W", SizeScale = "WAIST" },
                new Category { Code = "W-OUTER", Name = "Coats & Jackets", ParentId = Root("WOMEN"), ErpItemGroup = "OUT-W", SizeScale = "ALPHA" },
                new Category { Code = "M-SHIRT", Name = "Shirts", ParentId = Root("MEN"), ErpItemGroup = "APP-M", SizeScale = "ALPHA" },
                new Category { Code = "M-KNIT", Name = "Knitwear", ParentId = Root("MEN"), ErpItemGroup = "APP-M", SizeScale = "ALPHA" },
                new Category { Code = "M-JEANS", Name = "Jeans", ParentId = Root("MEN"), ErpItemGroup = "DNM-M", SizeScale = "WAIST" },
                new Category { Code = "M-OUTER", Name = "Coats & Jackets", ParentId = Root("MEN"), ErpItemGroup = "OUT-M", SizeScale = "ALPHA" },
                new Category { Code = "K-TOPS", Name = "Tops", ParentId = Root("KIDS"), ErpItemGroup = "APP-K", SizeScale = "KIDS" },
                new Category { Code = "A-BAGS", Name = "Bags", ParentId = Root("ACC"), ErpItemGroup = "ACC", SizeScale = "ONE" },
                new Category { Code = "A-SCARF", Name = "Scarves & Hats", ParentId = Root("ACC"), ErpItemGroup = "ACC", SizeScale = "ONE" });
            await db.SaveChangesAsync();
        }
    }
}
