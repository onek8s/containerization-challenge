using PartsCatalog.Models;

namespace PartsCatalog.Data;

public static class SeedData
{
    public static void Initialize(CatalogContext db)
    {
        db.Database.EnsureCreated();

        if (db.Parts.Any())
        {
            return;
        }

        var now = DateTime.UtcNow;
        db.Parts.AddRange(
            new Part { PartNumber = "BRG-6204-2RS", Description = "Deep groove ball bearing 20x47x14", Category = "Bearings", QuantityOnHand = 140, ReorderLevel = 40, UnitPrice = 4.85m, BinLocation = "A-01-03", LastUpdated = now },
            new Part { PartNumber = "BRG-6305-ZZ", Description = "Deep groove ball bearing 25x62x17", Category = "Bearings", QuantityOnHand = 22, ReorderLevel = 30, UnitPrice = 7.10m, BinLocation = "A-01-04", LastUpdated = now },
            new Part { PartNumber = "BLT-A42", Description = "V-belt classic section A, 42 in", Category = "Belts", QuantityOnHand = 35, ReorderLevel = 10, UnitPrice = 9.40m, BinLocation = "B-02-01", LastUpdated = now },
            new Part { PartNumber = "FLT-HYD-10", Description = "Hydraulic return filter element 10 micron", Category = "Filters", QuantityOnHand = 8, ReorderLevel = 12, UnitPrice = 38.00m, BinLocation = "C-04-02", LastUpdated = now },
            new Part { PartNumber = "SEAL-OR-032", Description = "O-ring NBR 32x3", Category = "Seals", QuantityOnHand = 600, ReorderLevel = 200, UnitPrice = 0.35m, BinLocation = "D-01-07", LastUpdated = now },
            new Part { PartNumber = "MTR-0.75KW-4P", Description = "Three-phase motor 0.75 kW 4-pole B3", Category = "Motors", QuantityOnHand = 3, ReorderLevel = 2, UnitPrice = 212.50m, BinLocation = "E-01-01", LastUpdated = now },
            new Part { PartNumber = "CPL-JAW-24", Description = "Jaw coupling hub 24 mm bore", Category = "Couplings", QuantityOnHand = 18, ReorderLevel = 6, UnitPrice = 14.75m, BinLocation = "B-05-02", LastUpdated = now },
            new Part { PartNumber = "FUSE-10A-GG", Description = "Cartridge fuse 10A gG 10x38", Category = "Electrical", QuantityOnHand = 95, ReorderLevel = 50, UnitPrice = 1.20m, BinLocation = "F-02-11", LastUpdated = now });
        db.SaveChanges();
    }
}
