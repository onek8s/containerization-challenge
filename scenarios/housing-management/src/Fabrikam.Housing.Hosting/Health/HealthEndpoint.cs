using System;
using System.Reflection;
using System.Threading.Tasks;
using Fabrikam.Housing.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace Fabrikam.Housing.Hosting.Health
{
    public static class HealthEndpoint
    {
        /// <summary>GET /health: version, machine name and a database round trip (used by the load balancer monitor).</summary>
        public static IApplicationBuilder UseHousingHealth(this IApplicationBuilder app)
        {
            return app.Map("/health", branch => branch.Run(async context =>
            {
                var db = context.RequestServices.GetRequiredService<HousingDbContext>();
                bool database;
                try
                {
                    database = await db.Database.CanConnectAsync();
                }
                catch (Exception)
                {
                    database = false;
                }
                context.Response.StatusCode = database ? 200 : 503;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new
                {
                    status = database ? "ok" : "database unavailable",
                    version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(),
                    machine = Environment.MachineName,
                    time = DateTime.Now
                }));
            }));
        }
    }
}
