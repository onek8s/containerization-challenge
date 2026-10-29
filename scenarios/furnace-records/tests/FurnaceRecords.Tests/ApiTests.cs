using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FurnaceRecords.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FurnaceRecords.Tests;

public sealed class ApiFactory : WebApplicationFactory<FurnaceRecords.Api.ApiKeyAuthenticationHandler>
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"fr-api-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:FurnaceRecords", $"Data Source={_file}");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_file);
    }
}

public class ApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ApiTests(ApiFactory factory) => _factory = factory;

    private HttpClient Client()
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add("X-Api-Key", DbInitializer.SampleApiKey);
        return c;
    }

    [Fact]
    public async Task Requires_api_key()
    {
        var c = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/furnaces")).StatusCode);
        c.DefaultRequestHeaders.Add("X-Api-Key", "fr_wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/furnaces")).StatusCode);
    }

    [Fact]
    public async Task Plans_and_reads_a_load()
    {
        var c = Client();
        var response = await c.PostAsJsonAsync("/api/v1/loads", new
        {
            furnace = "F01",
            recipe = "ST-2024",
            parts = new[] { new { workOrder = "WO-2001", partNumber = "8B1120-11", lot = "L7", quantity = 6 } },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var load = await response.Content.ReadFromJsonAsync<JsonElement>();
        var number = load.GetProperty("loadNumber").GetString()!;
        Assert.Equal("Planned", load.GetProperty("status").GetString());
        Assert.Equal($"/api/v1/loads/{number}", response.Headers.Location!.OriginalString);

        var read = await c.GetFromJsonAsync<JsonElement>($"/api/v1/loads/{number}");
        Assert.Equal("8B1120-11", read.GetProperty("parts")[0].GetProperty("partNumber").GetString());

        Assert.Equal(HttpStatusCode.Conflict, (await c.GetAsync($"/api/v1/loads/{number}/certificate")).StatusCode);
        var csv = await c.GetStringAsync($"/api/v1/loads/{number}/samples.csv");
        Assert.StartsWith("Time;Control;", csv);
    }

    [Fact]
    public async Task Invalid_load_is_422()
    {
        var response = await Client().PostAsJsonAsync("/api/v1/loads", new { furnace = "F02", recipe = "ST-7075", parts = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("not qualified", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Unknown_load_is_404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await Client().GetAsync("/api/v1/loads/F01-26-99999")).StatusCode);

    [Fact]
    public async Task Lists_reference_data()
    {
        var c = Client();
        Assert.Equal(2, (await c.GetFromJsonAsync<JsonElement>("/api/v1/furnaces")).GetArrayLength());
        Assert.Equal(6, (await c.GetFromJsonAsync<JsonElement>("/api/v1/recipes")).GetArrayLength());
    }
}
