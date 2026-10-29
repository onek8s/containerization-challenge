using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Integration.Messaging;
using Proseware.ControlTower.Tower.Models;
using Xunit;

namespace Proseware.ControlTower.Tests.Tower;

public sealed class TowerApiTests : IClassFixture<TowerApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly TowerApiFactory _factory;

    public TowerApiTests(TowerApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Dashboard_summary_counts_open_shipments()
    {
        var client = _factory.CreateClient();

        var summary = await client.GetFromJsonAsync<DashboardSummary>("/api/dashboard/summary", Json);

        Assert.NotNull(summary);
        Assert.True(summary!.OpenShipments > 0);
        Assert.Equal(summary.OpenShipments, summary.ByStatus.Sum(s => s.Count));
        Assert.Equal(summary.OpenShipments, summary.ByMode.Sum(s => s.Count));
        Assert.Equal(Enum.GetValues<Severity>().Length, summary.OpenAlertsBySeverity.Count);
        Assert.InRange(summary.OnTimeDeliveryPercent7d, 0, 100);
    }

    [Fact]
    public async Task Shipment_search_and_detail()
    {
        var client = _factory.CreateClient();

        var page = await client.GetFromJsonAsync<PagedResult<ShipmentRow>>("/api/shipments?openOnly=true&pageSize=5", Json);
        Assert.NotNull(page);
        Assert.InRange(page!.Items.Count, 1, 5);
        Assert.All(page.Items, s => Assert.NotEqual(ShipmentStatus.Delivered, s.Status));

        var detail = await client.GetFromJsonAsync<ShipmentDetail>($"/api/shipments/{page.Items[0].Id}", Json);
        Assert.Equal(page.Items[0].Number, detail!.Number);
        Assert.NotEmpty(detail.Legs);
        Assert.Equal(detail.Legs.Count, detail.Legs.Select(l => l.Sequence).Distinct().Count());

        var delivered = await client.GetFromJsonAsync<PagedResult<ShipmentRow>>("/api/shipments?status=Delivered&pageSize=1", Json);
        var deliveredDetail = await client.GetFromJsonAsync<ShipmentDetail>($"/api/shipments/{delivered!.Items[0].Id}", Json);
        Assert.Contains(deliveredDetail!.Milestones, m => m.Code == MilestoneCodes.Delivered);

        var missing = await client.GetAsync("/api/shipments/999999");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Resolving_an_alert_closes_it_and_publishes_AlertClosed()
    {
        var client = _factory.CreateClient();
        var bus = _factory.Services.GetRequiredService<InMemoryEventBus>();

        var alerts = await client.GetFromJsonAsync<List<AlertRow>>("/api/alerts?openOnly=true", Json);
        Assert.NotEmpty(alerts!);
        var alert = alerts![0];

        var ack = await client.PostAsync($"/api/alerts/{alert.Id}/acknowledge", null);
        ack.EnsureSuccessStatusCode();
        var acknowledged = await ack.Content.ReadFromJsonAsync<AlertRow>(Json);
        Assert.Equal("dev.operator", acknowledged!.AcknowledgedBy);

        var resolve = await client.PostAsJsonAsync($"/api/alerts/{alert.Id}/resolve", new ResolveAlertRequest("Carrier confirmed new delivery time"), Json);
        resolve.EnsureSuccessStatusCode();
        var resolved = await resolve.Content.ReadFromJsonAsync<AlertRow>(Json);
        Assert.NotNull(resolved!.ResolvedAt);
        Assert.Equal("Carrier confirmed new delivery time", resolved.Resolution);

        Assert.Contains(bus.Published.OfType<AlertClosed>(), e => e.AlertId == alert.Id);

        var open = await client.GetFromJsonAsync<List<AlertRow>>("/api/alerts?openOnly=true", Json);
        Assert.DoesNotContain(open!, a => a.Id == alert.Id);
    }

    [Fact]
    public async Task Map_and_terminal_workload()
    {
        var client = _factory.CreateClient();

        var map = await client.GetFromJsonAsync<MapData>("/api/dashboard/map", Json);
        Assert.NotEmpty(map!.Vehicles);
        Assert.Contains(map.Locations, l => l.Kind == LocationKind.Terminal);

        var terminals = await client.GetFromJsonAsync<List<TerminalWorkload>>("/api/kpi/terminals?hours=6", Json);
        Assert.Equal(4, terminals!.Count);
        Assert.All(terminals, t => Assert.Equal(6, t.Hours.Count));

        var carriers = await client.GetFromJsonAsync<List<CarrierScore>>("/api/kpi/carriers?days=60", Json);
        Assert.Contains(carriers!, c => c.Legs > 0);
    }

    [Fact]
    public async Task Spa_config_is_anonymous_and_health_answers()
    {
        var client = _factory.CreateClient();
        var config = await client.GetFromJsonAsync<JsonElement>("/api/config");
        Assert.Equal("tower-spa", config.GetProperty("clientId").GetString());
        Assert.True(config.GetProperty("developmentSignIn").GetBoolean());

        var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }
}
