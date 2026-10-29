using Fabrikam.Housing.Data;
using Fabrikam.Housing.DocumentService.Intake;
using Fabrikam.Housing.DocumentService.Storage;
using Fabrikam.Housing.Documents;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;

namespace Fabrikam.Housing.DocumentService
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
            AuditWriter.ApplicationName = "DocumentService";
            var storage = Configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();
            services.AddSingleton(storage);
            services.Configure<IntakeOptions>(Configuration.GetSection("Intake"));

            services.AddHousingData(Configuration);
            services.AddDocumentIndexing(storage.IndexFolder);
            services.AddSingleton<DocumentStore>();
            services.AddHostedService<IntakeService>();
            services.AddHostedService<ReindexService>();

            services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = storage.MaxUploadBytes);
            services.AddControllers().AddNewtonsoftJson();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment()) app.UseDeveloperExceptionPage();

            app.Map("/health", branch => branch.Run(async context =>
            {
                var store = context.RequestServices.GetRequiredService<DocumentStore>();
                var status = store.Health();
                context.Response.StatusCode = status.Ok ? 200 : 503;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonConvert.SerializeObject(status));
            }));

            app.UseMiddleware<ApiKeyMiddleware>();
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }
    }
}
