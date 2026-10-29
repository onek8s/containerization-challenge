using System.Net;
using System.Net.Sockets;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VanArsdel.Integration.Contracts.Erp;
using VanArsdel.Integration.LabStubs.Pipelines;
using VanArsdel.Integration.LabStubs.Simulators;

namespace VanArsdel.Integration.LabStubs.Lab
{
    /// <summary>
    /// One Kestrel process with every HTTP endpoint of the lab: the ERP order management service (CoreWCF,
    /// basicHttp), our AS2 receive URL and the partners' AS2 stations. Listens on loopback only.
    /// </summary>
    public sealed class LabHost : IAsyncDisposable
    {
        public const string ErpServicePath = "/erp/OrderManagementService.svc";

        private WebApplication _app;
        private System.ServiceModel.ChannelFactory<IOrderManagementService> _erpFactory;

        public LabHost(LabContext lab, int port = 0)
        {
            Lab = lab;
            Port = port == 0 ? FreePort() : port;
            Http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            Erp = new ErpSystem(lab);
            ErpService = new ErpOrderService(lab.Seed, Erp);
            Partners = new PartnerSimulator(lab, Http, () => BaseUrl);
            Platform = new LabPlatform(lab, new PipelineRunner(lab), new LabTransport(lab, Http, () => BaseUrl));
            Orchestrations = new LabOrchestrations(lab, Platform, ErpClient);
        }

        public LabContext Lab { get; }
        public int Port { get; }
        public string BaseUrl => "http://127.0.0.1:" + Port;
        public HttpClient Http { get; }
        public ErpSystem Erp { get; }
        public ErpOrderService ErpService { get; }
        public PartnerSimulator Partners { get; }
        public LabPlatform Platform { get; }
        public LabOrchestrations Orchestrations { get; }

        public async Task StartAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = Lab.Work });
            builder.Logging.ClearProviders();
            builder.WebHost.UseKestrel(k => k.Listen(IPAddress.Loopback, Port));
            builder.Services.AddServiceModelServices();
            builder.Services.AddSingleton(ErpService);

            _app = builder.Build();
            _app.UseServiceModel(b =>
            {
                b.AddService<ErpOrderService>();
                b.AddServiceEndpoint<ErpOrderService, IOrderManagementService>(new CoreWCF.BasicHttpBinding(), ErpServicePath);
            });

            _app.MapPost("/b2b/as2", async context =>
            {
                var body = await ReadBody(context.Request);
                var mdn = Platform.ReceiveAs2(context.Request.Headers["AS2-From"], context.Request.Headers["AS2-To"], context.Request.Headers["Message-ID"], body);
                await WriteMdn(context.Response, mdn);
            });

            _app.MapPost("/partners/{as2id}/as2", async context =>
            {
                var body = await ReadBody(context.Request);
                var mdn = Partners.HandleAs2(context.Request.Headers["AS2-To"], context.Request.Headers["AS2-From"], context.Request.Headers["Message-ID"], body);
                await WriteMdn(context.Response, mdn);
            });

            await _app.StartAsync();
        }

        /// <summary>The WCF-Custom send port ERP.OrderManagement as a client channel.</summary>
        public IOrderManagementService ErpClient()
        {
            _erpFactory ??= new System.ServiceModel.ChannelFactory<IOrderManagementService>(
                new System.ServiceModel.BasicHttpBinding(),
                new System.ServiceModel.EndpointAddress(BaseUrl + ErpServicePath));
            return _erpFactory.CreateChannel();
        }

        public async ValueTask DisposeAsync()
        {
            if (_erpFactory != null)
            {
                try
                {
                    _erpFactory.Close();
                }
                catch (System.ServiceModel.CommunicationException)
                {
                    _erpFactory.Abort();
                }
            }

            if (_app != null)
            {
                await _app.StopAsync();
                await _app.DisposeAsync();
            }

            Http.Dispose();
        }

        private static async Task<byte[]> ReadBody(HttpRequest request)
        {
            using var ms = new MemoryStream();
            await request.Body.CopyToAsync(ms);
            return ms.ToArray();
        }

        private static async Task WriteMdn(HttpResponse response, string mdn)
        {
            response.ContentType = "multipart/report; report-type=disposition-notification";
            await response.WriteAsync(mdn);
        }

        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
