using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using VanArsdel.Contracts.Orders;
using VanArsdel.Integration.Payments;
using VanArsdel.Orders.Api.Data;
using VanArsdel.Orders.Api.Domain;
using Xunit;

namespace VanArsdel.IntegrationTests;

public class OrdersFactory : ServiceFactory<OrderService, OrdersDbContext>
{
    public OrdersFactory() : base("Orders")
    {
    }
}

public class OrdersApiTests : IClassFixture<OrdersFactory>
{
    private const string StoreStaff = "fi012.staff; roles=Retail.StoreStaff; store=FI012";
    private const string CustomerService = "cs; roles=Retail.CustomerService";
    private readonly OrdersFactory _factory;

    public OrdersApiTests(OrdersFactory factory)
    {
        _factory = factory;
    }

    private static object StoreOrder(string store = "FI012") => new
    {
        storeCode = store,
        market = "FI",
        posReceiptNo = "FI012-T1-000777",
        customerEmail = "Aino.Virtanen@example.com ",
        firstName = "Aino",
        lastName = "Virtanen",
        deliveryMethod = DeliveryMethods.Home,
        shipTo = new { name = "Aino Virtanen", street = "Mannerheimintie 1", postalCode = "00100", city = "Helsinki", country = "FI" },
        lines = new[] { new { sku = "va1001-blk-m", quantity = 2, unitPrice = 59.90m, discount = 10m } },
    };

    private static async Task<JsonElement> WaitForAsync(HttpClient client, string url, Func<JsonElement, bool> done)
    {
        JsonElement last = default;
        for (var i = 0; i < 60; i++)
        {
            last = await client.GetFromJsonAsync<JsonElement>(url);
            if (done(last)) return last;
            await Task.Delay(250);
        }
        return last;
    }

    private static string SagaState(JsonElement order) =>
        order.TryGetProperty("saga", out var saga) && saga.ValueKind == JsonValueKind.Object ? saga.GetProperty("currentState").GetString() : null;

    [Fact]
    public async Task Health_endpoint_is_anonymous()
    {
        var response = await _factory.ClientAs(null).GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Orders_need_a_signed_in_user()
    {
        var response = await _factory.ClientAs(null).GetAsync("/api/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Store_staff_creates_an_endless_aisle_order_that_goes_to_allocation()
    {
        var client = _factory.ClientAs(StoreStaff);

        var response = await client.PostAsJsonAsync("/api/orders/store", StoreOrder());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(109.80m, created.GetProperty("total").GetDecimal());
        var orderNo = created.GetProperty("orderNo").GetString();

        var order = await WaitForAsync(client, "/api/orders/" + orderNo, o => SagaState(o) == "Allocating");
        Assert.Equal("Allocating", SagaState(order));
        Assert.Equal("aino.virtanen@example.com", order.GetProperty("customerEmail").GetString());
        Assert.Equal("VA1001-BLK-M", order.GetProperty("lines")[0].GetProperty("sku").GetString());
        Assert.Equal("Paid", order.GetProperty("paymentStatus").GetString());
    }

    [Fact]
    public async Task Store_staff_cannot_order_for_another_store()
    {
        var response = await _factory.ClientAs(StoreStaff).PostAsJsonAsync("/api/orders/store", StoreOrder("SE003"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Customer_service_cannot_create_store_orders()
    {
        var response = await _factory.ClientAs(CustomerService).PostAsJsonAsync("/api/orders/store", StoreOrder());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_order_is_a_problem_details_404()
    {
        var response = await _factory.ClientAs(CustomerService).GetAsync("/api/orders/W404404");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Psp_authorization_webhook_moves_a_web_order_to_allocation()
    {
        var webOrderNo = Random.Shared.Next(100000, 999999).ToString();
        using (var scope = _factory.Services.CreateScope())
        {
            var orders = scope.ServiceProvider.GetRequiredService<OrderService>();
            await orders.CreateFromWebShopAsync(new WebShopOrderSubmitted
            {
                WebOrderNo = webOrderNo,
                Market = "FI",
                Customer = new OrderCustomer { Email = "web@example.com", FirstName = "Web" },
                DeliveryMethod = DeliveryMethods.Home,
                ShippingAddress = new OrderAddress { Name = "Web Customer", Street = "Aleksanterinkatu 5", PostalCode = "00100", City = "Helsinki", Country = "FI" },
                PaymentReference = "pay_" + webOrderNo,
                PaymentMethod = "card",
                Lines = { new OrderLineInfo { Sku = "VA1001-BLK-M", Quantity = 1, UnitPrice = 59.90m } },
            }, CancellationToken.None);
        }
        var client = _factory.ClientAs(CustomerService);
        var before = await WaitForAsync(client, "/api/orders/W" + webOrderNo, o => SagaState(o) == "AwaitingPayment");
        Assert.Equal("AwaitingPayment", SagaState(before));

        var body = JsonSerializer.Serialize(new { id = "evt_" + webOrderNo, type = PspWebhookTypes.Authorized, paymentId = "pay_" + webOrderNo, amount = 5990, currency = "EUR" });
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/psp") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("Psp-Signature", WebhookSignature.Create("psp-dev-secret", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), body));
        var response = await _factory.ClientAs(null).SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var after = await WaitForAsync(client, "/api/orders/W" + webOrderNo, o => SagaState(o) == "Allocating");
        Assert.Equal("Allocating", SagaState(after));
    }

    [Fact]
    public async Task Psp_webhook_with_a_bad_signature_is_refused()
    {
        var body = "{\"id\":\"evt_bad\",\"type\":\"payment.authorized\",\"paymentId\":\"pay_x\"}";
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/psp") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("Psp-Signature", WebhookSignature.Create("wrong-secret", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), body));

        var response = await _factory.ClientAs(null).SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
