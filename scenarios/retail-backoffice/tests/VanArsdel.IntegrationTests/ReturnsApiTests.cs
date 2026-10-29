using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VanArsdel.Returns.Api.Data;
using VanArsdel.Returns.Api.Domain;
using Xunit;

namespace VanArsdel.IntegrationTests;

public class ReturnsFactory : ServiceFactory<ReturnService, ReturnsDbContext>
{
    public ReturnsFactory() : base("Returns")
    {
    }
}

public class ReturnsApiTests : IClassFixture<ReturnsFactory>
{
    private readonly ReturnsFactory _factory;

    public ReturnsApiTests(ReturnsFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Returns_need_a_signed_in_user()
    {
        var response = await _factory.ClientAs(null).GetAsync("/api/returns");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Finance_can_list_returns()
    {
        var list = await _factory.ClientAs("finance; roles=Retail.Finance").GetFromJsonAsync<JsonElement>("/api/returns");

        Assert.Equal(JsonValueKind.Array, list.ValueKind);
    }

    [Fact]
    public async Task Finance_cannot_register_returns()
    {
        var response = await _factory.ClientAs("finance; roles=Retail.Finance").PostAsJsonAsync("/api/returns", new { orderNo = "W1" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Store_staff_cannot_take_returns_for_another_store()
    {
        var response = await _factory.ClientAs("fi012.staff; roles=Retail.StoreStaff; store=FI012")
            .PostAsJsonAsync("/api/returns/store/SE003", new { @return = new { orderNo = "W1", lines = new[] { new { sku = "A", quantity = 1, reason = "Size" } } } });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Carrier_webhook_checks_the_token()
    {
        var response = await _factory.ClientAs(null).PostAsJsonAsync("/webhooks/carrier", new { trackingNumber = "JJFI1", @event = "InTransit" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
