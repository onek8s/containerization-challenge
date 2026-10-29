using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace VanArsdel.IntegrationTests;

/// <summary>
/// Runs a service in the Development environment on its own SQLite file and the in-memory bus,
/// with the test authentication scheme in front.
/// </summary>
public class ServiceFactory<TEntryPoint, TContext> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
    where TContext : DbContext
{
    private readonly string _connectionName;
    private readonly string _databaseFile = Path.Combine(Path.GetTempPath(), "vanarsdel-it-" + Guid.NewGuid().ToString("N") + ".db");

    public ServiceFactory(string connectionName)
    {
        _connectionName = connectionName;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:" + _connectionName, "Data Source=" + _databaseFile);
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("Messaging:Transport", "InMemory");
        builder.UseSetting("WebShopBus:Transport", "InMemory");
        builder.UseSetting("Redis:Configuration", "");
        builder.UseSetting("Outbox:PollIntervalMs", "250");

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            services.PostConfigure<AuthenticationOptions>(o =>
            {
                o.DefaultScheme = TestAuthHandler.SchemeName;
                o.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                o.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TContext>().Database.EnsureCreated();
        return host;
    }

    public HttpClient ClientAs(string user)
    {
        var client = CreateClient();
        if (user != null) client.DefaultRequestHeaders.Add(TestAuthHandler.Header, user);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            base.Dispose(disposing);
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is System.Threading.Channels.ChannelClosedException))
        {
            // The MassTransit 7 in-memory transport sometimes completes a queue twice when the host stops
            // right after it started. Only the test host does that.
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_databaseFile)) File.Delete(_databaseFile);
    }
}
