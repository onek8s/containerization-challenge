using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace VanArsdel.StoreManager
{
    /// <summary>
    /// Serves the React app and its runtime configuration. No server-side logic: the app talks to
    /// the gateway directly with the user's token.
    /// </summary>
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHealthChecks();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (!env.IsDevelopment())
            {
                app.UseHsts();
            }

            var appFolder = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "app");
            Directory.CreateDirectory(appFolder);
            var files = new PhysicalFileProvider(appFolder);

            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = files,
                OnPrepareResponse = ctx =>
                {
                    // Hashed bundles can be cached for ever; index.html never (new releases).
                    ctx.Context.Response.Headers["Cache-Control"] = ctx.File.Name == "index.html" ? "no-cache" : "public,max-age=31536000,immutable";
                },
            });

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHealthChecks("/health");
                endpoints.MapGet("/config.json", async context =>
                {
                    var spa = Configuration.GetSection("Spa");
                    context.Response.Headers["Cache-Control"] = "no-store";
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        authority = spa["Authority"],
                        clientId = spa["ClientId"],
                        scope = spa["Scope"],
                        apiBaseUrl = spa["ApiBaseUrl"],
                        hubUrl = spa["HubUrl"],
                    }));
                });
                // Client-side routes (/tasks, /callback, ...) all get index.html.
                endpoints.MapFallback(async context =>
                {
                    context.Response.ContentType = "text/html";
                    context.Response.Headers["Cache-Control"] = "no-cache";
                    await context.Response.SendFileAsync(files.GetFileInfo("index.html"));
                });
            });
        }
    }
}
