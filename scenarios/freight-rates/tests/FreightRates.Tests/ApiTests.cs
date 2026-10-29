using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FreightRates.Core;
using FreightRates.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreightRates.Tests;

public sealed class ApiTests : IClassFixture<ApiTests.Factory>
{
    private readonly Factory _factory;

    public ApiTests(Factory factory) => _factory = factory;

    private HttpClient Client(string? apiKey = DbInitializer.SampleApiKey)
    {
        var client = _factory.CreateClient();
        if (apiKey is not null)
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return client;
    }

    private static object Request(string destination = "30301") => new
    {
        originZip = "60601",
        destinationZip = destination,
        shipDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
        lines = new[] { new { freightClass = 70m, weightLbs = 2000 } },
        accessorials = new[] { "APPT" },
        customerReference = "PO-7781",
    };

    [Fact]
    public async Task Requires_api_key()
    {
        var response = await Client(apiKey: null).PostAsJsonAsync("/api/v1/quotes", Request());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        response = await Client("fr_wrong").GetAsync("/api/v1/quotes");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Creates_and_reads_quote()
    {
        var client = Client();
        var response = await client.PostAsJsonAsync("/api/v1/quotes", Request());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var number = body.GetProperty("quoteNumber").GetString();
        Assert.Equal(1000m, body.GetProperty("grossCharge").GetDecimal());
        Assert.Equal("TEST0001", body.GetProperty("tariff").GetString());

        var read = await client.GetFromJsonAsync<JsonElement>($"/api/v1/quotes/{number}");
        Assert.Equal("PO-7781", read.GetProperty("customerReference").GetString());

        // Other customers cannot read it.
        var other = await Client("fr_dev_nwind_51e09d3c").GetAsync($"/api/v1/quotes/{number}");
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    }

    [Fact]
    public async Task Invalid_shipment_returns_validation_problem()
    {
        var response = await Client().PostAsJsonAsync("/api/v1/quotes", new
        {
            originZip = "1",
            destinationZip = "30301",
            shipDate = DateOnly.FromDateTime(DateTime.Today),
            lines = Array.Empty<object>(),
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Origin ZIP code must be 5 digits.", body);
    }

    [Fact]
    public async Task Engine_errors_map_to_status_codes()
    {
        _factory.Engine.Failure = new RatingException(RatingErrorKind.PointNotServed, -303, "Point not served");
        try
        {
            var response = await Client().PostAsJsonAsync("/api/v1/quotes", Request());
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

            _factory.Engine.Failure = new RatingException(RatingErrorKind.EngineUnavailable, -101, "Licence key not found");
            response = await Client().PostAsJsonAsync("/api/v1/quotes", Request());
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.DoesNotContain("Licence", await response.Content.ReadAsStringAsync());
        }
        finally
        {
            _factory.Engine.Failure = null;
        }
    }

    [Fact]
    public async Task Estimates_freight_class()
    {
        var body = await Client().GetFromJsonAsync<JsonElement>("/api/v1/freight-class?weightLbs=500&lengthIn=48&widthIn=40&heightIn=40");
        Assert.Equal(92.5m, body.GetProperty("freightClass").GetDecimal());
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"freightrates-test-{Guid.NewGuid():N}.db");

        public FakeRatingEngine Engine { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:FreightRates", $"Data Source={_dbPath}");
            builder.UseSetting("Database:Provider", "Sqlite");
            builder.UseSetting("Database:SeedSampleData", "true");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRatingEngine>();
                services.AddSingleton<IRatingEngine>(Engine);
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);
        }
    }
}
