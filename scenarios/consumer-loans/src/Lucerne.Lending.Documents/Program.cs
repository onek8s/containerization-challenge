using Lucerne.Lending.Data;
using Lucerne.Lending.Documents;
using Lucerne.Lending.Integration;
using NLog.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(o => o.ServiceName = "LucerneLendingDocuments")
    .UseNLog()
    .ConfigureServices((context, services) =>
    {
        services.AddLendingData(context.Configuration);
        services.AddLendingIntegrations(context.Configuration);
        services.AddSingleton<DocumentJobs>();
        services.AddSingleton<SigningCompletion>();
        services.AddSingleton<Notifications>();
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
