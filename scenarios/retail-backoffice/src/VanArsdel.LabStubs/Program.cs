using MassTransit;
using VanArsdel.BuildingBlocks.Hosting;
using VanArsdel.BuildingBlocks.Messaging;
using VanArsdel.LabStubs.Oidc;
using VanArsdel.LabStubs.Stubs;
using VanArsdel.LabStubs.Tools;

// Usage:
//   VanArsdel.LabStubs                         serve the external system stand-ins on http://localhost:5100
//   VanArsdel.LabStubs seed [--styles 40] [--customers 40]
//   VanArsdel.LabStubs webshop [--orders 5] [--delivery Home|ParcelLocker|ClickAndCollect] [--store FI012] [--decline]
//   VanArsdel.LabStubs pos [--stores FI012,SE003] [--date 2026-10-29] [--receipts 25] [--inbox folder]
//   VanArsdel.LabStubs pack [--collect]
//   VanArsdel.LabStubs token <user>             print an access token for curl
var command = args.FirstOrDefault(a => !a.StartsWith("-")) ?? "serve";

if (command != "serve")
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
        .AddEnvironmentVariables()
        .Build();
    try
    {
        switch (command)
        {
            case "seed":
                return await new Seeder(configuration).RunAsync(args);
            case "pos":
                return await new PosGenerator(configuration).RunAsync(args);
            case "pack":
                return await new Packer(configuration).RunAsync(args);
            case "token":
                var http = new HttpClient();
                var response = await http.PostAsync((configuration["Lab:Issuer"] ?? "http://localhost:5100") + "/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "password", ["client_id"] = "labtools", ["client_secret"] = LabUsers.ClientSecret,
                    ["username"] = args.Skip(1).FirstOrDefault() ?? "admin", ["password"] = LabUsers.Password,
                }));
                var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Console.WriteLine(json.RootElement.GetProperty("access_token").GetString());
                return 0;
            case "webshop":
                var options = configuration.GetSection("WebShopBus").Get<MessagingOptions>() ?? new MessagingOptions();
                using (var host = Host.CreateDefaultBuilder().ConfigureServices(s => s.AddMassTransit(x => MessagingExtensions.ConfigureTransport(x, options, null))).Build())
                {
                    var bus = host.Services.GetRequiredService<IBusControl>();
                    await bus.StartAsync();
                    try
                    {
                        return await new WebShopSimulator(configuration, bus).RunAsync(args);
                    }
                    finally
                    {
                        await bus.StopAsync();
                    }
                }
            default:
                Console.Error.WriteLine("Unknown command " + command);
                return 2;
        }
    }
    catch (HttpRequestException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

return VanArsdelHost.RunWeb("labstubs", args, a =>
{
    var builder = WebApplication.CreateBuilder(a);
    builder.Host.UseVanArsdelSerilog("labstubs");
    builder.Services.AddHttpClient("webhooks", c => c.Timeout = TimeSpan.FromSeconds(30));
    builder.Services.AddSingleton<LabIssuer>();
    builder.Services.AddSingleton<ErpStub>();
    builder.Services.AddSingleton<CarrierStub>();
    builder.Services.AddSingleton<PspStub>();
    builder.Services.AddSingleton<PostiqStub>();
    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

    var app = builder.Build();
    app.UseCors();
    app.MapGet("/", () => Results.Content(
        "<h2>VanArsdel lab stubs</h2><ul><li><a href=\"/.well-known/openid-configuration\">OIDC</a></li><li><a href=\"/erp/admin/state\">ERP</a></li>" +
        "<li><a href=\"/postiq/messages\">Mail and SMS</a></li></ul>", "text/html"));
    app.MapGet("/health", () => "Healthy");
    app.Services.GetRequiredService<LabIssuer>().Map(app);
    app.Services.GetRequiredService<ErpStub>().Map(app);
    app.Services.GetRequiredService<CarrierStub>().Map(app);
    app.Services.GetRequiredService<PspStub>().Map(app);
    app.Services.GetRequiredService<PostiqStub>().Map(app);
    return app;
});
