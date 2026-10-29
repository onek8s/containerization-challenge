using System.Globalization;
using System.Text;

namespace VanArsdel.LabStubs.Tools;

/// <summary>"pos": writes till export files (TLOG v3) for a few stores into the POS feed inbox.</summary>
public class PosGenerator
{
    // Decimal comma like the tills, but an ASCII minus: with ICU, fi-FI formats negatives with U+2212.
    private static readonly NumberFormatInfo Fi = new() { NumberDecimalSeparator = ",", NegativeSign = "-" };
    private readonly IConfiguration _configuration;

    public PosGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<int> RunAsync(string[] args)
    {
        var client = new LabClient(_configuration, "admin");
        var stores = (Seeder.Value(args, "--stores") ?? "FI012,FI001,SE003,EE001").Split(',');
        var date = DateTime.TryParse(Seeder.Value(args, "--date"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : DateTime.Today;
        var receiptsPerStore = int.TryParse(Seeder.Value(args, "--receipts"), out var r) ? r : 25;
        var inbox = Seeder.Value(args, "--inbox") ?? _configuration["Lab:PosInbox"] ?? "../../.devdata/pos/inbox";
        Directory.CreateDirectory(inbox);

        var products = await client.GetAsync("catalog/products?status=Published&pageSize=200");
        var styles = new List<(string Sku, decimal Eur, decimal Sek)>();
        foreach (var p in products.GetProperty("items").EnumerateArray().Take(30))
        {
            var detail = await client.GetAsync($"catalog/products/{p.GetProperty("styleCode").GetString()}");
            foreach (var sku in detail.GetProperty("skus").EnumerateArray())
            {
                styles.Add((sku.GetProperty("skuCode").GetString(), detail.GetProperty("recommendedPriceEur").GetDecimal(), detail.GetProperty("recommendedPriceSek").GetDecimal()));
            }
        }

        var rnd = new Random();
        foreach (var store in stores)
        {
            var currency = store.StartsWith("SE") ? "SEK" : "EUR";
            var vat = store[..2] switch { "SE" => 0.25m, "EE" => 0.20m, "LV" or "LT" => 0.21m, _ => 0.24m };
            var sb = new StringBuilder();
            var sequence = Directory.GetFiles(inbox, $"POS_{store}_{date:yyyyMMdd}_*.txt").Length + 1 + rnd.Next(100, 900);
            sb.Append($"# TLOG export v3 {store}\r\nH;{store};{date:yyyyMMdd};{sequence:000};{currency}\r\n");
            decimal grand = 0;
            for (var i = 1; i <= receiptsPerStore; i++)
            {
                var isReturn = rnd.Next(12) == 0;
                var lines = Enumerable.Range(0, 1 + rnd.Next(3)).Select(_ => styles[rnd.Next(styles.Count)]).ToList();
                var lineRows = new List<string>();
                decimal total = 0;
                foreach (var (sku, eur, sek) in lines)
                {
                    var price = currency == "SEK" ? sek : eur;
                    var discount = rnd.Next(5) == 0 ? Math.Round(price * 0.2m, 2) : 0m;
                    var lineTotal = price - discount;
                    total += lineTotal;
                    lineRows.Add($"L;{sku};{(isReturn ? -1 : 1)};{F(price)};{F(discount)};{F(isReturn ? -lineTotal : lineTotal)};{(discount > 0 ? "AUTUMN20" : "")}");
                }
                var card = rnd.Next(3) == 0 ? "6438" + rnd.Next(10000000, 99999999) : "";
                var time = date.AddHours(10 + rnd.Next(9)).AddMinutes(rnd.Next(60)).AddSeconds(rnd.Next(60));
                sb.Append($"R;{i:000000};T{1 + rnd.Next(3)};{time:yyyy-MM-dd HH:mm:ss};C{rnd.Next(100, 999)};{card};{(isReturn ? 1 : 0)};{F(total)};{F(Math.Round(total - total / (1 + vat), 2))}\r\n");
                foreach (var row in lineRows) sb.Append(row).Append("\r\n");
                var tender = rnd.Next(4) == 0 ? "CASH" : "CARD";
                sb.Append($"P;{tender};{F(total)}\r\n");
                grand += isReturn ? -total : total;
            }
            sb.Append($"T;{receiptsPerStore};{F(grand)}\r\n");

            var file = Path.Combine(inbox, $"POS_{store}_{date:yyyyMMdd}_{sequence:000}.txt");
            // Write under a temp name and rename, like the store routers do.
            await File.WriteAllTextAsync(file + ".part", sb.ToString(), Encoding.Latin1);
            File.Move(file + ".part", file, true);
            Console.WriteLine($"{Path.GetFileName(file)} {receiptsPerStore} receipts {F(grand)} {currency}");
        }
        return 0;
    }

    private static string F(decimal value) => value.ToString("0.00", Fi);
}
