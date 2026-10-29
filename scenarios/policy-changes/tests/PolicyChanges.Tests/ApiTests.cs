using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using PolicyChanges.Data;

namespace PolicyChanges.Tests;

public sealed class ApiTests : IDisposable
{
    private readonly string _dbFile = Path.Combine(Path.GetTempPath(), $"policychanges-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public ApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:PolicyChanges", $"Data Source={_dbFile}");
            b.UseSetting("Database:Provider", "Sqlite");
            b.UseSetting("Database:SeedSampleData", "true");
        });
    }

    public void Dispose()
    {
        _factory.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_dbFile);
    }

    private HttpClient Client(string? key = DbInitializer.SamplePartnerKey)
    {
        var client = _factory.CreateClient();
        if (key is not null) client.DefaultRequestHeaders.Add("X-Api-Key", key);
        return client;
    }

    private static object Address(string reference) => new
    {
        policyNumber = "HL-1042118",
        changeType = "AddressChange",
        effectiveDate = DateTime.Today.ToString("yyyy-MM-dd"),
        partnerReference = reference,
        fields = new Dictionary<string, string> { ["Street"] = "Hämeenkatu 1", ["PostalCode"] = "33100", ["City"] = "Tampere", ["Country"] = "FI" },
    };

    [Fact]
    public async Task Requires_known_api_key()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client(null).GetAsync("/api/v1/changes")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client("pc_wrong").GetAsync("/api/v1/changes")).StatusCode);
    }

    [Fact]
    public async Task Submit_returns_reference_and_status()
    {
        var client = Client();
        var post = await client.PostAsJsonAsync("/api/v1/changes", Address("NB-1001"));
        Assert.Equal(HttpStatusCode.Accepted, post.StatusCode);
        var created = await post.Content.ReadFromJsonAsync<JsonElement>(Json);
        var reference = created.GetProperty("reference").GetString();
        Assert.StartsWith("PC-", reference);
        Assert.Equal($"/api/v1/changes/{reference}", post.Headers.Location!.ToString());

        var get = await client.GetFromJsonAsync<JsonElement>($"/api/v1/changes/{reference}", Json);
        Assert.Equal("Queued", get.GetProperty("status").GetString());
        Assert.Equal("NB-1001", get.GetProperty("partnerReference").GetString());

        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/changes?partnerReference=NB-1001", Json);
        Assert.Equal(1, list.GetArrayLength());
    }

    [Fact]
    public async Task Invalid_change_returns_problem_details()
    {
        var post = await Client().PostAsJsonAsync("/api/v1/changes", new
        {
            policyNumber = "HL-1042119",
            changeType = "PaymentMethodChange",
            effectiveDate = DateTime.Today.ToString("yyyy-MM-dd"),
            fields = new Dictionary<string, string> { ["PaymentMethod"] = "DirectDebit", ["Iban"] = "FI00", ["BillingDay"] = "31" },
        });
        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
        var body = await post.Content.ReadAsStringAsync();
        Assert.Contains("check digit", body);
        Assert.Contains("IBAN is not valid.", body);
        Assert.Contains("Billing day must be 1-28.", body);
    }

    [Fact]
    public async Task Partner_reference_is_accepted_once_and_others_cannot_see_it()
    {
        var post = await Client().PostAsJsonAsync("/api/v1/changes", Address("NB-2002"));
        var reference = (await post.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("reference").GetString();

        var again = await Client().PostAsJsonAsync("/api/v1/changes", Address("NB-2002"));
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);

        var other = Client("pc_dev_autohuset_9e04b7d1");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/changes/{reference}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/v1/changes/{reference}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await Client().DeleteAsync($"/api/v1/changes/{reference}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client().DeleteAsync($"/api/v1/changes/{reference}")).StatusCode);
    }

    [Fact]
    public async Task Health_reports_database()
    {
        var response = await _factory.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
