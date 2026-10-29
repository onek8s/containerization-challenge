using System;
using Fabrikam.Housing.Hosting;
using Fabrikam.Housing.Hosting.Health;
using Fabrikam.Housing.Hosting.Security;
using Fabrikam.Housing.Jobs.Infrastructure;
using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Fabrikam.Housing.Jobs
{
    public class Startup
    {
        public const string AdminPolicy = "JobsAdmin";

        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public IConfiguration Configuration { get; }
        public IWebHostEnvironment Environment { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHousingCore(Configuration, typeof(Startup).Assembly);
            services.AddHousingDataProtection(Configuration, "jobs");
            services.AddHousingJobs(Configuration);
            services.AddHousingSignIn(Configuration, "Fabrikam.Jobs");
            services.AddAuthorization(options =>
                options.AddPolicy(AdminPolicy, p => p.RequireRole(HousingRoles.Admin, HousingRoles.Finance)));

            var jobs = Configuration.GetSection("Jobs");
            services.AddHangfire(config =>
            {
                config.SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                    .UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings();
                if (string.Equals(Configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase))
                {
                    config.UseMemoryStorage();
                }
                else
                {
                    config.UseSqlServerStorage(Configuration.GetConnectionString("Housing"), new SqlServerStorageOptions
                    {
                        SchemaName = "HangFire",
                        PrepareSchemaIfNecessary = jobs.GetValue("PrepareSchemaIfNecessary", false),
                        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                        QueuePollInterval = TimeSpan.Zero,
                        UseRecommendedIsolationLevel = true,
                        DisableGlobalLocks = true
                    });
                }
            });
            services.AddHangfireServer(options =>
            {
                options.ServerName = System.Environment.MachineName + ":housing-jobs";
                options.WorkerCount = jobs.GetValue("WorkerCount", 4);
            });
        }

        public void Configure(IApplicationBuilder app, IRecurringJobManager recurringJobs, IOptions<JobsOptions> options)
        {
            if (Environment.IsDevelopment())
            {
                DevelopmentDatabase.EnsureSeeded(app.ApplicationServices);
                app.UseDeveloperExceptionPage();
            }

            app.UseHousingHealth();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            // Send people to the identity server before Hangfire's own authorization filter runs.
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/hangfire") && context.User.Identity?.IsAuthenticated != true)
                {
                    await context.ChallengeAsync();
                    return;
                }
                await next();
            });
            app.UseHangfireDashboard("/hangfire", new DashboardOptions
            {
                Authorization = new[] { new DashboardAuthorizationFilter() },
                DashboardTitle = "Fabrikam Housing jobs",
                AppPath = Configuration["Jobs:BackOfficeUrl"] ?? "/"
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", context =>
                {
                    context.Response.Redirect("/hangfire");
                    return System.Threading.Tasks.Task.CompletedTask;
                });
                endpoints.MapPost("/jobs/{name}/run", async context =>
                {
                    var name = (string)context.Request.RouteValues["name"];
                    if (JobCatalog.Find(name) == null)
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        return;
                    }
                    context.RequestServices.GetRequiredService<IRecurringJobManager>().Trigger(name);
                    context.Response.StatusCode = StatusCodes.Status202Accepted;
                    await context.Response.WriteAsync("Triggered " + name);
                }).RequireAuthorization(AdminPolicy);
            });

            JobCatalog.RegisterAll(recurringJobs, options.Value);
        }
    }
}
