using System.Net;
using System.Text.Json;

namespace VanArsdel.LabStubs.Tools;

/// <summary>"seed": stores, products, opening stock and club members through the public APIs, like the go-live migration did.</summary>
public class Seeder
{
    private readonly LabClient _admin;
    private readonly LabClient _merch;
    private readonly IConfiguration _configuration;

    public Seeder(IConfiguration configuration)
    {
        _configuration = configuration;
        _admin = new LabClient(configuration, "admin");
        _merch = new LabClient(configuration, "merch");
    }

    public async Task<int> RunAsync(string[] args)
    {
        var styleCount = int.TryParse(Value(args, "--styles"), out var s) ? s : 40;
        var customerCount = int.TryParse(Value(args, "--customers"), out var c) ? c : 40;

        await SeedStoresAsync();
        var skus = await SeedProductsAsync(styleCount);
        Console.WriteLine("Waiting for inventory to learn the stores...");
        var expected = SeedData.Stores().Count + 1;
        await WaitForAsync(async () => (await _admin.GetAsync("inventory/locations")).GetArrayLength() >= expected, TimeSpan.FromSeconds(90));
        await SeedStockAsync(skus);
        await SeedCustomersAsync(customerCount);
        Console.WriteLine("Seed done.");
        return 0;
    }

    private async Task SeedStoresAsync()
    {
        var stores = SeedData.Stores();
        var created = 0;
        await _merch.SendJsonAsync(HttpMethod.Put, "storeops/stores/DC01", new
        {
            name = "Distribution centre Vantaa", type = "DC", country = "FI", city = "Vantaa", street = "Logistiikkatie 8", postalCode = "01510",
            shipFromStore = true, clickAndCollect = false, openingHours = "Mon-Sun 0-24",
        });
        foreach (var batch in stores.Chunk(10))
        {
            await Task.WhenAll(batch.Select(async st =>
            {
                await _merch.SendJsonAsync(HttpMethod.Put, $"storeops/stores/{st.Code}", new
                {
                    name = st.Name, type = "Store", country = st.Country, city = st.City, street = st.Street, postalCode = st.PostalCode,
                    shipFromStore = st.ShipFromStore, clickAndCollect = st.ClickAndCollect, managerEmail = st.Code.ToLowerInvariant() + "@vanarsdel.example",
                    openingHours = "Mon-Fri 10-20; Sat 10-18; Sun 12-18", phone = "+358 10 555 " + st.Code[2..],
                }, HttpStatusCode.Conflict);
                Interlocked.Increment(ref created);
            }));
        }
        Console.WriteLine($"Stores: {created} + DC01");
    }

    private async Task<List<string>> SeedProductsAsync(int count)
    {
        var skus = new List<string>();
        var sequence = 50000;
        foreach (var style in SeedData.Styles(count))
        {
            var colours = SeedData.Colours.Skip(style.StyleCode.Length % 3).Take(style.Category.StartsWith("A-") ? 2 : 3).ToList();
            var skuInputs = new List<object>();
            foreach (var colour in colours)
            {
                foreach (var size in style.Sizes)
                {
                    skuInputs.Add(new { colourCode = colour.Code, colourName = colour.Name, size, ean = SeedData.Ean(sequence++), costPrice = Math.Round(style.PriceEur * 0.32m, 2), weightKg = 0.4m });
                    skus.Add($"{style.StyleCode}-{colour.Code}-{size}");
                }
            }
            var response = await _merch.SendAsync(HttpMethod.Post, "catalog/products", new
            {
                styleCode = style.StyleCode, name = $"{style.Name} {style.StyleCode[^2..]}", description = $"{style.Name} from the {style.Season} collection.",
                brandCode = style.Brand, categoryCode = style.Category, season = style.Season, gender = style.Gender, material = "See care label",
                vatClass = style.Category.StartsWith("K-") ? "Children" : "Standard",
                recommendedPriceEur = style.PriceEur, recommendedPriceSek = Math.Round(style.PriceEur * 11.2m / 10) * 10 - 1,
                skus = skuInputs, publish = true,
            });
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Conflict)
            {
                Console.WriteLine($"  {style.StyleCode}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            }
        }
        Console.WriteLine($"Products: {count} styles, {skus.Count} SKUs");
        return skus;
    }

    private async Task SeedStockAsync(List<string> skus)
    {
        var rnd = new Random(42);
        await _admin.SendJsonAsync(HttpMethod.Post, "inventory/stock/DC01/counts?reference=go-live",
            skus.Select(s => new { sku = s, counted = 40 + rnd.Next(60), minLevel = 10, maxLevel = 120 }).ToList());

        var stores = SeedData.Stores().Where(s => s.ShipFromStore || s.Code.EndsWith("01") || s.Code is "FI012" or "SE003").ToList();
        foreach (var store in stores)
        {
            await _admin.SendJsonAsync(HttpMethod.Post, $"inventory/stock/{store.Code}/counts?reference=go-live",
                skus.Where((_, i) => (i + store.Code.GetHashCode()) % 3 != 0)
                    .Select(s => new { sku = s, counted = rnd.Next(0, 6), minLevel = 2, maxLevel = 6 }).ToList());
        }
        Console.WriteLine($"Stock: DC01 and {stores.Count} stores");
    }

    private async Task SeedCustomersAsync(int count)
    {
        var rnd = new Random(7);
        var markets = new[] { "FI", "FI", "FI", "SE", "SE", "EE", "LV", "LT" };
        for (var i = 1; i <= count; i++)
        {
            var first = SeedData.FirstNames[rnd.Next(SeedData.FirstNames.Length)];
            var last = SeedData.LastNames[rnd.Next(SeedData.LastNames.Length)];
            var market = markets[i % markets.Length];
            await _admin.SendJsonAsync(HttpMethod.Post, "customers/customers", new
            {
                email = $"customer{i:000}@example.com", firstName = first, lastName = last, phone = $"+358 40 {1000000 + i}", market,
                preferredStore = market + "001", emailConsent = i % 3 != 0, smsConsent = i % 4 == 0, source = "seed",
            }, HttpStatusCode.Conflict);
        }
        Console.WriteLine($"Customers: {count}");
    }

    private static async Task WaitForAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            try
            {
                if (await condition()) return;
            }
            catch (HttpRequestException)
            {
                // not there yet
            }
            await Task.Delay(2000);
        }
        throw new TimeoutException("Timed out waiting for the services to catch up.");
    }

    public static string Value(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
