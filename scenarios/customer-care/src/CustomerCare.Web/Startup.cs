using System;
using System.Configuration;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Owin;
using Owin;
using System.Web;
using Tailspin.CustomerCare.Web.Jobs;
using Tailspin.CustomerCare.Web.Security;

[assembly: OwinStartup(typeof(Tailspin.CustomerCare.Web.Startup))]

namespace Tailspin.CustomerCare.Web
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            GlobalConfiguration.Configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage("CustomerCare", new SqlServerStorageOptions
                {
                    SchemaName = "HangFire",
                    PrepareSchemaIfNecessary = true,
                    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                    QueuePollInterval = TimeSpan.Zero,
                    DisableGlobalLocks = true
                });

            var workers = int.Parse(ConfigurationManager.AppSettings["CustomerCare:JobWorkers"] ?? "2");
            app.UseHangfireServer(new BackgroundJobServerOptions
            {
                ServerName = Environment.MachineName + ":care",
                WorkerCount = workers,
                Queues = new[] { "default", "mail" }
            });

            app.UseHangfireDashboard("/admin/jobs", new DashboardOptions
            {
                Authorization = new[] { new SupervisorDashboardFilter() },
                AppPath = VirtualPathUtility.ToAbsolute("~/"),
                DisplayStorageConnectionString = false
            });

            JobSchedule.Register();

            app.MapSignalR();
        }
    }
}
