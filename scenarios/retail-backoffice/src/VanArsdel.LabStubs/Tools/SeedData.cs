namespace VanArsdel.LabStubs.Tools;

/// <summary>Store network and assortment used by the seed and the simulators.</summary>
public static class SeedData
{
    public record StoreSeed(string Code, string Name, string Country, string City, string Street, string PostalCode, bool ShipFromStore, bool ClickAndCollect);

    private static readonly Dictionary<string, (int Count, string[] Cities)> Countries = new()
    {
        ["FI"] = (50, new[] { "Helsinki", "Espoo", "Tampere", "Vantaa", "Oulu", "Turku", "Jyväskylä", "Lahti", "Kuopio", "Pori", "Joensuu", "Lappeenranta", "Vaasa", "Seinäjoki", "Rovaniemi" }),
        ["SE"] = (35, new[] { "Stockholm", "Göteborg", "Malmö", "Uppsala", "Västerås", "Örebro", "Linköping", "Helsingborg", "Norrköping", "Umeå" }),
        ["EE"] = (12, new[] { "Tallinn", "Tartu", "Pärnu", "Narva" }),
        ["LV"] = (10, new[] { "Riga", "Daugavpils", "Liepāja", "Jelgava" }),
        ["LT"] = (12, new[] { "Vilnius", "Kaunas", "Klaipėda", "Šiauliai" }),
    };

    private static readonly string[] Malls = { "Citycenter", "Forum", "Station", "Galleria", "Retail Park", "High Street", "Outlet Village", "Mall" };

    public static List<StoreSeed> Stores()
    {
        var stores = new List<StoreSeed>();
        foreach (var (country, (count, cities)) in Countries)
        {
            for (var i = 1; i <= count; i++)
            {
                var city = cities[(i - 1) % cities.Length];
                var mall = Malls[(i * 7) % Malls.Length];
                stores.Add(new StoreSeed(
                    $"{country}{i:000}", $"VanArsdel {city} {mall}", country, city, $"{mall} {i}", $"{(i * 137) % 90000 + 10000}",
                    ShipFromStore: i % 5 == 2 || (country == "FI" && i == 12), ClickAndCollect: i % 7 != 0));
            }
        }
        return stores;
    }

    public record Colour(string Code, string Name);

    public static readonly Colour[] Colours =
    {
        new("BLK", "Black"), new("NAV", "Navy"), new("GRY", "Grey melange"), new("WHT", "White"), new("BEI", "Beige"), new("RED", "Brick red"), new("GRN", "Forest green"),
    };

    public record StyleSeed(string StyleCode, string Name, string Brand, string Category, string Season, string Gender, decimal PriceEur, string[] Sizes);

    private static readonly (string Category, string Gender, string[] Names, string[] Sizes, decimal Price)[] Assortment =
    {
        ("W-DRESS", "W", new[] { "Wrap dress", "Shirt dress", "Knit dress", "Midi dress" }, new[] { "XS", "S", "M", "L", "XL" }, 69.95m),
        ("W-KNIT", "W", new[] { "Merino jumper", "Cable cardigan", "Rib knit top" }, new[] { "XS", "S", "M", "L", "XL" }, 59.95m),
        ("W-JEANS", "W", new[] { "Straight jeans", "Slim jeans", "Wide leg jeans" }, new[] { "26", "28", "30", "32", "34" }, 79.95m),
        ("W-OUTER", "W", new[] { "Wool coat", "Rain jacket", "Down parka" }, new[] { "XS", "S", "M", "L", "XL" }, 189.00m),
        ("M-SHIRT", "M", new[] { "Oxford shirt", "Flannel shirt", "Linen shirt" }, new[] { "S", "M", "L", "XL", "XXL" }, 49.95m),
        ("M-KNIT", "M", new[] { "Crew neck jumper", "Half-zip jumper" }, new[] { "S", "M", "L", "XL", "XXL" }, 64.95m),
        ("M-JEANS", "M", new[] { "Regular jeans", "Tapered jeans" }, new[] { "30", "32", "34", "36", "38" }, 79.95m),
        ("M-OUTER", "M", new[] { "Field jacket", "Wool overcoat", "Shell jacket" }, new[] { "S", "M", "L", "XL", "XXL" }, 199.00m),
        ("K-TOPS", "K", new[] { "Kids T-shirt", "Kids hoodie" }, new[] { "110", "122", "134", "146" }, 19.95m),
        ("A-BAGS", "U", new[] { "Leather tote", "Canvas backpack" }, new[] { "ONE" }, 89.00m),
        ("A-SCARF", "U", new[] { "Wool scarf", "Silk scarf" }, new[] { "ONE" }, 34.95m),
    };

    private static readonly string[] Brands = { "VA", "VAS", "NRD", "LMN", "KOT" };

    public static List<StyleSeed> Styles(int count)
    {
        var styles = new List<StyleSeed>();
        for (var i = 0; i < count; i++)
        {
            var a = Assortment[i % Assortment.Length];
            var name = a.Names[(i / Assortment.Length) % a.Names.Length];
            var brand = Brands[i % Brands.Length];
            styles.Add(new StyleSeed($"{brand}{1001 + i}", name, brand, a.Category, i % 3 == 0 ? "CORE" : "AW26", a.Gender, a.Price + (i % 4) * 5, a.Sizes));
        }
        return styles;
    }

    /// <summary>EAN-13 in the company's GS1 range (same scheme as the catalog's own numbering).</summary>
    public static string Ean(int sequence)
    {
        var body = "6438521" + (sequence % 100000).ToString("00000");
        var sum = 0;
        for (var i = 0; i < 12; i++) sum += (body[i] - '0') * (i % 2 == 0 ? 1 : 3);
        return body + ((10 - sum % 10) % 10);
    }

    public static readonly string[] FirstNames = { "Aino", "Eero", "Linnea", "Oskar", "Maja", "Elias", "Kerli", "Rasmus", "Laura", "Mantas", "Ieva", "Jānis", "Emma", "Leo", "Saga" };
    public static readonly string[] LastNames = { "Virtanen", "Korhonen", "Andersson", "Johansson", "Tamm", "Saar", "Bērziņš", "Kazlauskas", "Nieminen", "Lindqvist" };
}
