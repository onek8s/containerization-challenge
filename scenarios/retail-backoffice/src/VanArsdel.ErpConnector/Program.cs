using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VanArsdel.BuildingBlocks.Data;
using VanArsdel.BuildingBlocks.Hosting;
using VanArsdel.BuildingBlocks.Messaging;
using VanArsdel.ErpConnector.Consumers;
using VanArsdel.ErpConnector.Data;
using VanArsdel.ErpConnector.Security;
using VanArsdel.ErpConnector.Workers;
using VanArsdel.Integration.Erp;

namespace VanArsdel.ErpConnector
{
    public class Program
    {
        public static int Main(string[] args)
        {
            if (VanArsdelHost.HasSwitch(args, "--protect-password"))
            {
                // Run once per server as an administrator: --protect-password <password> [--file D:\VanArsdel\Secrets\erp.bin]
                var file = VanArsdelHost.SwitchValue(args, "--file") ?? @"D:\VanArsdel\Secrets\erp.bin";
                ErpPassword.Protect(VanArsdelHost.SwitchValue(args, "--protect-password"), file);
                Console.WriteLine("Protected password written to " + file);
                return 0;
            }

            return VanArsdelHost.Run("erpconnector", args, CreateHostBuilder,
                (host, a) => host.PrepareDatabase<ErpDbContext>(a));
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService(o => o.ServiceName = "VanArsdelErpConnector")
                .UseVanArsdelSerilog("erpconnector")
                .ConfigureServices((context, services) =>
                {
                    var configuration = context.Configuration;
                    services.AddServiceDatabase<ErpDbContext>(configuration, "ErpConnector");
                    services.Configure<ErpClientSettings>(configuration.GetSection("Erp"));
                    services.Configure<ErpConnectorOptions>(configuration.GetSection("ErpConnector"));
                    services.AddSingleton(_ => new ErpCredentials(ErpPassword.Resolve(configuration)));
                    services.AddScoped<ErpGateway>();

                    services.AddVanArsdelMessaging(configuration, "erp", x =>
                    {
                        x.AddConsumer<ItemConsumer>();
                        x.AddConsumer<SalesJournalConsumer>();
                        x.AddConsumer<InvoiceConsumer>();
                        x.AddConsumer<TransferConsumer>();
                    });
                    services.AddHostedService<GoodsReceiptPoller>();
                });
    }
}
