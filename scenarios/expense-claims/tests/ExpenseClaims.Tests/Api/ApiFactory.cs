using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseClaims.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ExpenseClaims.Tests.Api;

/// <summary>Runs the API with test sign-in, the development seed data and an in-memory SQLite database.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly string _root = Path.Combine(Path.GetTempPath(), "expense-claims-api-tests", Guid.NewGuid().ToString("N"));

    public ApiFactory() => _connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string>
        {
            ["ConnectionStrings:ExpenseClaims"] = "DataSource=:memory:",
            ["Database:Provider"] = "Sqlite",
            ["Database:MigrateOnStartup"] = "true",
            ["Database:SeedTestData"] = "true",
            ["Authentication:Mode"] = "Development",
            ["Authentication:Development:SigningKey"] = "integration-test-signing-key-0123456789",
            ["Authentication:Development:Roles:mikko.salonen@lamnahealthcare.com:0"] = "Finance",
            ["Authentication:Development:Roles:laura.niemi@lamnahealthcare.com:0"] = "Administrator",
            ["Receipts:RootPath"] = Path.Combine(_root, "receipts"),
            ["Serilog:WriteTo:0:Args:path"] = Path.Combine(_root, "logs", "api-.log")
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ExpenseClaimsDbContext>>();
            services.AddDbContext<ExpenseClaimsDbContext>(o => o.UseSqlite(_connection));
        });
    }

    public async Task<HttpClient> ClientForAsync(string user)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/dev/token", new { upn = $"{user}@lamnahealthcare.com" });
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.access_token);
        return client;
    }

    private sealed record TokenResponse(string access_token);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
