using Refit;
using VanArsdel.BuildingBlocks.Caching;
using VanArsdel.BuildingBlocks.Data;
using VanArsdel.BuildingBlocks.Hosting;
using VanArsdel.BuildingBlocks.Http;
using VanArsdel.BuildingBlocks.Messaging;
using VanArsdel.BuildingBlocks.Outbox;
using VanArsdel.BuildingBlocks.Web;
using VanArsdel.Integration.Carrier;
using VanArsdel.Integration.Payments;
using VanArsdel.Returns.Api;
using VanArsdel.Returns.Api.Clients;
using VanArsdel.Returns.Api.Data;
using VanArsdel.Returns.Api.Domain;

return VanArsdelHost.RunWeb("returns-api", args, a => BuildApp(a), (host, a) => host.PrepareDatabase<ReturnsDbContext>(a));

static WebApplication BuildApp(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseVanArsdelSerilog("returns-api");
    var configuration = builder.Configuration;

    builder.Services.AddVanArsdelApi(configuration, "VanArsdel Returns API", typeof(Policies).Assembly, Policies.Configure);
    builder.Services.AddServiceDatabase<ReturnsDbContext>(configuration, "Returns");
    builder.Services.AddVanArsdelCaching(configuration, "returns");
    builder.Services.AddVanArsdelMessaging(configuration, "returns");
    builder.Services.AddEfOutbox<ReturnsDbContext>();
    builder.Services.Configure<ReturnsOptions>(configuration.GetSection("Returns"));
    builder.Services.AddScoped<ReturnService>();

    builder.Services.AddServiceIdentity(configuration);
    builder.Services.AddServiceClient<IOrdersApi>(configuration, "Orders");
    builder.Services.AddRefitClientFor<ICarrierApi>(configuration["Carrier:BaseUrl"]);
    builder.Services.AddRefitClientFor<IPspApi>(configuration["Psp:BaseUrl"])
        .ConfigureHttpClient(c => c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(configuration["Psp:MerchantId"] + ":" + configuration["Psp:MerchantKey"]))))
        .ConfigurePrimaryHttpMessageHandler(() => PspCertificate.Handler(configuration));
    builder.Services.AddHealthChecks().AddDbContextCheck<ReturnsDbContext>("database");

    var app = builder.Build();
    app.UseVanArsdelApi(app.Environment);
    return app;
}

/// <summary>Returns loads the PSP merchant certificate from a PFX file (copied to the server by operations).</summary>
internal static class PspCertificate
{
    public static HttpMessageHandler Handler(IConfiguration configuration)
    {
        var handler = new HttpClientHandler();
        var path = configuration["Psp:CertificatePath"];
        if (!string.IsNullOrEmpty(path))
        {
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.ClientCertificates.Add(new System.Security.Cryptography.X509Certificates.X509Certificate2(path, configuration["Psp:CertificatePassword"]));
        }
        return handler;
    }
}

public partial class Program
{
}
