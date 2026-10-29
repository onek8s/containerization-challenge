using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using ProductReturns.Core;
using ProductReturns.Data;
using ProductReturns.Jobs.Infrastructure;
using ProductReturns.Jobs.Jobs;

namespace ProductReturns.Jobs
{
    /// <summary>
    /// Scheduled jobs, started by Windows Task Scheduler on returns-jobs:
    ///   refunds    every weekday 06:30, refund batch file for finance
    ///   reminders  daily 09:00, reminds customers who have not sent their parcel
    ///   expire     daily 02:00, closes returns that were never sent
    ///   purge      Sundays 03:00, removes old labels and personal data
    /// </summary>
    public static class Program
    {
        public const string MachineSettingsFile = @"C:\ProgramData\WideWorldImporters\ProductReturns\jobs.settings.json";

        private static readonly string[] Verbs = { "refunds", "reminders", "expire", "purge" };

        public static int Main(string[] args)
        {
            var verb = args.FirstOrDefault()?.ToLowerInvariant();
            if (verb == null || !Verbs.Contains(verb))
            {
                Console.Error.WriteLine("Usage: ProductReturns.Jobs <" + string.Join("|", Verbs) + "> [--Key=Value ...]");
                return 2;
            }

            using (var host = CreateHostBuilder(args.Skip(1).ToArray(), verb).Build())
            {
                var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ProductReturns.Jobs");
                if (host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
                {
                    DatabaseSetup.EnsureDevelopmentDatabase(host.Services);
                }

                // Task Scheduler can start a second instance when a run takes longer than the interval.
                using (var mutex = new Mutex(false, @"Global\WWI.ProductReturns.Jobs." + verb))
                {
                    if (!mutex.WaitOne(TimeSpan.Zero))
                    {
                        logger.LogWarning("Job {Job} is already running, exiting", verb);
                        return 3;
                    }

                    try
                    {
                        using (var scope = host.Services.CreateScope())
                        {
                            IJob job = verb switch
                            {
                                "refunds" => scope.ServiceProvider.GetRequiredService<RefundExportJob>(),
                                "reminders" => scope.ServiceProvider.GetRequiredService<ReminderJob>(),
                                "expire" => scope.ServiceProvider.GetRequiredService<ExpireJob>(),
                                _ => scope.ServiceProvider.GetRequiredService<PurgeJob>()
                            };

                            logger.LogInformation("Job {Job} started", verb);
                            // Synchronous on purpose: the mutex must be released on the thread that owns it.
                            job.RunAsync(CancellationToken.None).GetAwaiter().GetResult();
                            logger.LogInformation("Job {Job} finished", verb);
                            return 0;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Job {Job} failed", verb);
                        return 1;
                    }
                    finally
                    {
                        mutex.ReleaseMutex();
                    }
                }
            }
        }

        private static IHostBuilder CreateHostBuilder(string[] args, string verb) =>
            Host.CreateDefaultBuilder(args)
                .UseContentRoot(AppContext.BaseDirectory)
                .ConfigureAppConfiguration((context, config) =>
                {
                    if (File.Exists(MachineSettingsFile))
                    {
                        config.AddJsonFile(MachineSettingsFile, optional: true, reloadOnChange: false);
                    }

                    config.AddCommandLine(args);
                })
                .ConfigureLogging((context, logging) =>
                {
                    logging.AddEventLog();
                    logging.AddProvider(new FileLoggerProvider(context.Configuration["Jobs:LogFolder"], verb));
                })
                .ConfigureServices((context, services) =>
                {
                    var configuration = context.Configuration;
                    services.Configure<EventLogSettings>(s =>
                    {
                        s.SourceName = "Product Returns Jobs";
                        s.LogName = "Application";
                    });

                    services.AddReturnsDatabase(configuration["Database:Provider"], configuration.GetConnectionString("Returns"));
                    services.Configure<JobOptions>(configuration.GetSection("Jobs"));
                    services.Configure<JobMailOptions>(configuration.GetSection("Mail"));
                    services.AddSingleton(configuration.GetSection("ReturnPolicy").Get<ReturnPolicyOptions>() ?? new ReturnPolicyOptions());
                    services.AddSingleton<BusinessCalendar>();
                    services.AddSingleton<JobMailer>();
                    services.AddTransient<RefundExportJob>();
                    services.AddTransient<ReminderJob>();
                    services.AddTransient<ExpireJob>();
                    services.AddTransient<PurgeJob>();
                });
    }
}
