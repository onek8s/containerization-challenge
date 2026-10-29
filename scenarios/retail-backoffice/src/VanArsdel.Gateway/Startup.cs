using System.Linq;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Polly;
using Serilog;
using VanArsdel.BuildingBlocks.Security;
using VanArsdel.BuildingBlocks.Web;

namespace VanArsdel.Gateway
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddVanArsdelJwt(Configuration);
            services.AddCors(o => o.AddPolicy("stores", p => p
                .WithOrigins(ApiExtensions.ConfiguredCorsOrigins(Configuration).ToArray())
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));
            services.AddHealthChecks();
            services.AddTransient<CorrelationHeaderHandler>();
            services.AddOcelot(Configuration)
                .AddPolly()
                .AddDelegatingHandler<CorrelationHeaderHandler>(true);
            services.Configure<ForwardedHeadersOptions>(o =>
            {
                o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                o.KnownNetworks.Clear();
                o.KnownProxies.Clear();
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseForwardedHeaders();
            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseSerilogRequestLogging();
            app.UseCors("stores");
            app.UseWebSockets();
            app.UseRouting();
            app.UseEndpoints(e =>
            {
                e.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse });
                e.MapGet("/", ctx => ctx.Response.WriteAsync("VanArsdel API gateway"));
            });
            app.UseOcelot().Wait();
        }
    }

    /// <summary>Passes the correlation id and the original client address to the services.</summary>
    public class CorrelationHeaderHandler : System.Net.Http.DelegatingHandler
    {
        private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _accessor;

        public CorrelationHeaderHandler(Microsoft.AspNetCore.Http.IHttpContextAccessor accessor)
        {
            _accessor = accessor;
        }

        protected override System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            var ctx = _accessor.HttpContext;
            if (ctx != null)
            {
                request.Headers.Remove(CorrelationIdMiddleware.HeaderName);
                request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, ctx.TraceIdentifier);
                request.Headers.TryAddWithoutValidation("X-Forwarded-For", ctx.Connection.RemoteIpAddress?.ToString());
            }
            return base.SendAsync(request, cancellationToken);
        }
    }
}
