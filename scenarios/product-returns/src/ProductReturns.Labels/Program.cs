using System;
using System.IO;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.EventLog;
using ProductReturns.Data;
using ProductReturns.Labels.Carriers;
using ProductReturns.Labels.Services;

namespace ProductReturns.Labels
{
    public class Program
    {
        /// <summary>Machine settings maintained by operations, outside the install folder.</summary>
        public const string MachineSettingsFile = @"C:\ProgramData\WideWorldImporters\ProductReturns\labels.settings.json";

        public static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "protect-secret")
            {
                return SecretProtector.Run(args);
            }

            var host = CreateHostBuilder(args).Build();
            if (host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
            {
                DatabaseSetup.EnsureDevelopmentDatabase(host.Services);
            }

            host.Run();
            return 0;
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureAppConfiguration((context, config) =>
                {
                    if (File.Exists(MachineSettingsFile))
                    {
                        config.AddJsonFile(MachineSettingsFile, optional: true, reloadOnChange: true);
                    }

                    config.AddCommandLine(args);
                })
                .ConfigureServices((context, services) =>
                {
                    var configuration = context.Configuration;
                    services.Configure<EventLogSettings>(s =>
                    {
                        s.SourceName = "Product Returns Labels";
                        s.LogName = "Application";
                    });

                    services.AddReturnsDatabase(configuration["Database:Provider"], configuration.GetConnectionString("Returns"));
                    services.Configure<LabelOptions>(configuration.GetSection("Labels"));
                    services.Configure<CarrierOptions>(configuration.GetSection("Carrier"));
                    services.Configure<MailOptions>(configuration.GetSection("Mail"));
                    services.AddSingleton<CarrierQuota>();
                    services.AddSingleton<LabelStore>();
                    services.AddSingleton<LabelMailer>();
                    services.AddScoped<LabelProcessor>();

                    if (string.Equals(configuration["Carrier:Provider"], "File", StringComparison.OrdinalIgnoreCase))
                    {
                        services.AddSingleton<ICarrierClient, FileCarrierClient>();
                    }
                    else
                    {
                        services.AddHttpClient<ICarrierClient, ParcelLinkClient>()
                            .ConfigurePrimaryHttpMessageHandler(() => CreateCarrierHandler(configuration));
                    }

                    services.AddHostedService<LabelWorker>();
                });

        /// <summary>Outbound calls go through the corporate proxy, which authenticates the service account.</summary>
        private static HttpMessageHandler CreateCarrierHandler(IConfiguration configuration)
        {
            var handler = new HttpClientHandler();
            var proxy = configuration["Carrier:Proxy"];
            if (!string.IsNullOrEmpty(proxy))
            {
                handler.Proxy = new WebProxy(proxy) { UseDefaultCredentials = true, BypassProxyOnLocal = true };
                handler.UseProxy = true;
                handler.DefaultProxyCredentials = CredentialCache.DefaultCredentials;
            }

            return handler;
        }
    }
}
