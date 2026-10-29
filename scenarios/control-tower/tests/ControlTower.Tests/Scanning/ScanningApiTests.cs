using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain.Events;
using Proseware.ControlTower.Integration.Messaging;
using Xunit;

namespace Proseware.ControlTower.Tests.Scanning;

public class ScanningApiTests : IClassFixture<ScanningApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly ScanningApiFactory _factory;

    public ScanningApiTests(ScanningApiFactory factory) => _factory = factory;

    private HttpClient Device()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Device-Key", ScanningApiFactory.DeviceHeader);
        return client;
    }

    private string KnownSscc()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ControlTowerDbContext>();
        return db.Packages.OrderBy(p => p.Id).Select(p => p.Sscc).First();
    }

    private sealed record Result(Guid ClientScanId, string Outcome, long? ScanId, string? ShipmentNumber, string? Message);

    [Fact]
    public async Task Requests_without_credentials_are_rejected()
    {
        var response = await _factory.CreateClient().GetAsync("/api/packages/" + KnownSscc());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_device_key_is_rejected()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Device-Key", "TC51-TEST-01:wrong");
        var response = await client.GetAsync("/api/packages/" + KnownSscc());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Package_lookup_accepts_application_identifier_prefix()
    {
        var sscc = KnownSscc();
        var response = await Device().GetAsync("/api/packages/00" + sscc);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(sscc, doc.RootElement.GetProperty("sscc").GetString());
        Assert.StartsWith("PF", doc.RootElement.GetProperty("shipmentNumber").GetString());
    }

    [Fact]
    public async Task Scans_are_recorded_once_and_published()
    {
        var sscc = KnownSscc();
        var id = Guid.NewGuid();
        var batch = new
        {
            deviceId = "TC51-TEST-01",
            scans = new object[]
            {
                new { clientScanId = id, barcode = "(00)" + sscc, kind = "Receive", terminal = "FIVAN", scannedAt = DateTimeOffset.UtcNow.AddMinutes(-3) },
                new { clientScanId = Guid.NewGuid(), barcode = "123456789012345678", kind = "Receive", terminal = "FIVAN", scannedAt = DateTimeOffset.UtcNow },
                new { clientScanId = Guid.NewGuid(), barcode = sscc, kind = "Receive", terminal = "XXNOPE", scannedAt = DateTimeOffset.UtcNow }
            }
        };
        var client = Device();

        var first = await (await client.PostAsJsonAsync("/api/scans", batch, Json)).Content.ReadFromJsonAsync<List<Result>>(Json);
        Assert.NotNull(first);
        Assert.Equal("Accepted", first!.Single(r => r.ClientScanId == id).Outcome);
        Assert.Contains(first, r => r.Outcome == "InvalidBarcode");
        Assert.Contains(first, r => r.Outcome == "UnknownTerminal");

        var again = await (await client.PostAsJsonAsync("/api/scans", new { deviceId = "TC51-TEST-01", scans = new[] { batch.scans[0] } }, Json))
            .Content.ReadFromJsonAsync<List<Result>>(Json);
        Assert.Equal("Duplicate", Assert.Single(again!).Outcome);

        var bus = _factory.Services.GetRequiredService<InMemoryEventBus>();
        Assert.Single(bus.Published.OfType<ScanRecorded>(), e => e.Sscc == sscc);
    }

    [Fact]
    public async Task Empty_batch_is_a_bad_request()
    {
        var response = await Device().PostAsJsonAsync("/api/scans", new { deviceId = "TC51-TEST-01", scans = Array.Empty<object>() }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Labels_are_printed_on_the_configured_printer()
    {
        var response = await Device().PostAsJsonAsync("/api/labels/print", new { printer = "zt-test", sscc = KnownSscc() }, Json);
        response.EnsureSuccessStatusCode();
        var zpl = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(_factory.LabelFolder, "*.zpl")));
        Assert.Contains(KnownSscc(), zpl);

        var unknown = await Device().PostAsJsonAsync("/api/labels/print", new { printer = "zt-missing", sscc = KnownSscc() }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }
}
