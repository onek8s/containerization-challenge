using System.Text.Json;
using MassTransit;
using VanArsdel.Contracts.Orders;

namespace VanArsdel.LabStubs.Tools;

/// <summary>"webshop": places orders the way the web shop platform does (queue message + PSP authorisation).</summary>
public class WebShopSimulator
{
    private readonly IConfiguration _configuration;
    private readonly ISendEndpointProvider _send;
    private readonly LabClient _client;

    public WebShopSimulator(IConfiguration configuration, ISendEndpointProvider send)
    {
        _configuration = configuration;
        _send = send;
        _client = new LabClient(configuration, "admin");
    }

    public async Task<int> RunAsync(string[] args)
    {
        var count = int.TryParse(Seeder.Value(args, "--orders"), out var n) ? n : 5;
        var delivery = Seeder.Value(args, "--delivery");
        var collectStore = Seeder.Value(args, "--store") ?? "FI012";
        var decline = args.Contains("--decline");

        var products = await _client.GetAsync("catalog/products?status=Published&pageSize=200");
        var styles = products.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("styleCode").GetString()).ToList();
        if (styles.Count == 0)
        {
            Console.Error.WriteLine("No published products. Run seed first.");
            return 1;
        }

        var rnd = new Random();
        var endpoint = await _send.GetSendEndpoint(new Uri("queue:" + (_configuration["WebShopBus:QueueName"] ?? "webshop-orders")));
        var http = new HttpClient();
        var pspBase = (_configuration["Lab:Issuer"] ?? "http://localhost:5100") + "/psp/lab/authorizations";

        for (var i = 0; i < count; i++)
        {
            var market = new[] { "FI", "FI", "FI", "SE", "EE" }[rnd.Next(5)];
            var method = delivery ?? new[] { DeliveryMethods.Home, DeliveryMethods.Home, DeliveryMethods.ParcelLocker, DeliveryMethods.ClickAndCollect }[rnd.Next(4)];
            var lines = new List<OrderLineInfo>();
            foreach (var style in styles.OrderBy(_ => rnd.Next()).Take(1 + rnd.Next(3)))
            {
                var product = await _client.GetAsync($"catalog/products/{style}");
                var skus = product.GetProperty("skus").EnumerateArray().ToList();
                var sku = skus[rnd.Next(skus.Count)].GetProperty("skuCode").GetString();
                var price = product.GetProperty(market == "SE" ? "recommendedPriceSek" : "recommendedPriceEur").GetDecimal();
                lines.Add(new OrderLineInfo { Sku = sku, Quantity = 1, UnitPrice = price, Discount = 0 });
            }

            var webOrderNo = market + DateTime.UtcNow.ToString("yyMMddHHmmss") + rnd.Next(100, 999);
            var currency = market == "SE" ? "SEK" : "EUR";
            var shipping = method == DeliveryMethods.ClickAndCollect ? 0m : market == "SE" ? 49m : 4.90m;
            var first = SeedData.FirstNames[rnd.Next(SeedData.FirstNames.Length)];
            var order = new WebShopOrderSubmitted
            {
                WebOrderNo = webOrderNo,
                Market = market,
                Currency = currency,
                PlacedUtc = DateTime.UtcNow,
                Customer = new OrderCustomer
                {
                    Email = $"{first.ToLowerInvariant()}.{rnd.Next(1000)}@example.com", FirstName = first,
                    LastName = SeedData.LastNames[rnd.Next(SeedData.LastNames.Length)], Phone = "040 " + rnd.Next(1000000, 9999999),
                    LoyaltyCardNo = Seeder.Value(args, "--card"),
                },
                DeliveryMethod = method,
                CollectStoreCode = method == DeliveryMethods.ClickAndCollect ? collectStore : null,
                ShippingAddress = method == DeliveryMethods.ClickAndCollect ? null : new OrderAddress
                {
                    Name = first, Street = "Testikatu " + rnd.Next(1, 80), PostalCode = "00" + rnd.Next(100, 990), City = "Helsinki", Country = market,
                },
                Lines = lines,
                ShippingFee = shipping,
                PaymentReference = "pay_" + Guid.NewGuid().ToString("N")[..20],
                PaymentMethod = "card",
            };
            await endpoint.Send(order);

            var total = lines.Sum(l => l.UnitPrice * l.Quantity - l.Discount) + shipping;
            var auth = await http.PostAsJsonAsync(pspBase, new
            {
                paymentId = order.PaymentReference, merchantReference = "W" + webOrderNo, amount = (long)Math.Round(total * 100), currency, decline,
            });
            auth.EnsureSuccessStatusCode();
            Console.WriteLine($"W{webOrderNo} {market} {method} {lines.Count} lines {total:0.00} {currency}");
        }
        return 0;
    }
}
