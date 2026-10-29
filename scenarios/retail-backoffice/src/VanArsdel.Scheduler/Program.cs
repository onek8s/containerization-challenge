using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using VanArsdel.BuildingBlocks.Hosting;
using VanArsdel.BuildingBlocks.Http;
using VanArsdel.BuildingBlocks.Security;
using VanArsdel.Scheduler.Jobs;

namespace VanArsdel.Scheduler
{
    public class Program
    {
        public static int Main(string[] args) => VanArsdelHost.Run("scheduler", args, CreateHostBuilder);

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService(o => o.ServiceName = "VanArsdelScheduler")
                .UseVanArsdelSerilog("scheduler")
                .ConfigureServices((context, services) =>
                {
                    var configuration = context.Configuration;
                    services.AddServiceIdentity(configuration);
                    services.AddHttpClient("services", c => c.Timeout = TimeSpan.FromMinutes(10))
                        .AddHttpMessageHandler<ClientCredentialsHandler>();

                    var jobs = configuration.GetSection("Scheduler:Jobs").Get<List<JobDefinition>>() ?? new List<JobDefinition>();
                    var timeZone = TimeZoneInfo.FindSystemTimeZoneById(configuration["Scheduler:TimeZone"] ?? "Europe/Helsinki");

                    services.AddQuartz(q =>
                    {
                        q.SchedulerName = "VanArsdel";
                        q.SchedulerId = "AUTO";
                        q.UseMicrosoftDependencyInjectionJobFactory();
                        q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 4);

                        // Both app03 nodes run the scheduler; the SQL job store makes sure each job fires once.
                        if (string.Equals(configuration["Scheduler:Store"], "SqlServer", StringComparison.OrdinalIgnoreCase))
                        {
                            q.UsePersistentStore(store =>
                            {
                                store.UseProperties = true;
                                store.UseClustering(c => c.CheckinInterval = TimeSpan.FromSeconds(20));
                                store.UseSqlServer(configuration.GetConnectionString("Quartz"));
                                store.UseJsonSerializer();
                            });
                        }

                        foreach (var job in jobs)
                        {
                            if (!job.Enabled) continue;
                            var key = new JobKey(job.Name, "services");
                            q.AddJob<ServiceCallJob>(key, j => j
                                .WithDescription(job.Description)
                                .UsingJobData(ServiceCallJob.ServiceKey, job.Service)
                                .UsingJobData(ServiceCallJob.PathKey, job.Path)
                                .StoreDurably());
                            q.AddTrigger(t => t
                                .ForJob(key)
                                .WithIdentity(job.Name + "-trigger", "services")
                                .WithCronSchedule(job.Cron, c => c.InTimeZone(timeZone).WithMisfireHandlingInstructionFireAndProceed()));
                        }
                    });
                    services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);
                });
    }
}
