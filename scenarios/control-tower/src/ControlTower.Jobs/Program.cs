using System.Text;
using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.SqlServer;
using Microsoft.Extensions.Options;
using NLog.Web;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Documents;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Integration.Messaging;
using Proseware.ControlTower.Jobs.Infrastructure;
using Proseware.ControlTower.Jobs.Jobs;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var logger = NLogBuilder.ConfigureNLog(Path.Combine(AppContext.BaseDirectory, "nlog.config")).GetCurrentClassLogger();
try
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    });
    builder.Host.UseWindowsService(o => o.ServiceName = "ControlTowerJobs");
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    var configuration = builder.Configuration;
    var services = builder.Services;
    services.Configure<JobsOptions>(configuration.GetSection(JobsOptions.Section));
    services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.Section));
    var jobsOptions = configuration.GetSection(JobsOptions.Section).Get<JobsOptions>() ?? new JobsOptions();

    services.AddControlTowerData(configuration);
    services.AddControlTowerMessaging(configuration);
    services.AddControlTowerDocuments(configuration);
    services.AddSingleton<IClock, SystemClock>();
    services.AddSingleton<Mailer>();
    services.AddScoped<KpiAggregationJob>();
    services.AddScoped<CustomerReportJob>();
    services.AddScoped<InvoiceExportJob>();
    services.AddScoped<DocumentPurgeJob>();
    services.AddScoped<PositionRetentionJob>();
    services.AddScoped<FuelSurchargeImportJob>();
    services.AddHostedService<DocumentRequestSubscriber>();

    services.AddHangfire(cfg =>
    {
        cfg.SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings();
        if (string.Equals(jobsOptions.Storage, "Memory", StringComparison.OrdinalIgnoreCase))
        {
            cfg.UseMemoryStorage();
        }
        else
        {
            cfg.UseSqlServerStorage(configuration.GetConnectionString("Hangfire") ?? configuration.GetConnectionString("ControlTower"), new SqlServerStorageOptions
            {
                SchemaName = "hangfire",
                PrepareSchemaIfNecessary = configuration.GetValue("Jobs:PrepareSchema", false),
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.FromSeconds(15),
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true
            });
        }
    });
    services.AddHangfireServer(o =>
    {
        o.ServerName = $"{Environment.MachineName}:jobs";
        o.WorkerCount = jobsOptions.WorkerCount;
        o.Queues = new[] { "default", "reports" };
    });

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        if (string.Equals(configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var file = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(configuration.GetConnectionString("ControlTower")).DataSource;
            var folder = Path.GetDirectoryName(Path.GetFullPath(file));
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        }
        await app.Services.EnsureDevelopmentDatabaseAsync(seed: true);
    }

    app.UseHangfireDashboard("/hangfire", new Hangfire.DashboardOptions
    {
        DashboardTitle = "Control Tower jobs",
        Authorization = new[] { new DashboardAuthorizationFilter(jobsOptions.Dashboard) },
        DisplayStorageConnectionString = false
    });
    app.MapGet("/", () => Results.Redirect("/hangfire"));
    app.MapGet("/health", (IOptions<JobsOptions> o) => Results.Ok(new { status = "Healthy", storage = o.Value.Storage, time = DateTimeOffset.UtcNow }));

    RegisterRecurringJobs(jobsOptions);
    app.Run();
}
catch (Exception ex)
{
    logger.Error(ex, "Control Tower jobs stopped because of an exception");
    throw;
}
finally
{
    NLog.LogManager.Shutdown();
}

static void RegisterRecurringJobs(JobsOptions o)
{
    var tz = LocalTime.Zone(o.ScheduleTimeZone);
    RecurringJob.AddOrUpdate<KpiAggregationJob>("kpi-aggregation", j => j.RunAsync(CancellationToken.None), o.KpiCron, tz);
    RecurringJob.AddOrUpdate<CustomerReportJob>("customer-reports", j => j.RunAsync(CancellationToken.None), o.CustomerReportCron, tz, "reports");
    RecurringJob.AddOrUpdate<InvoiceExportJob>("invoice-export", j => j.RunAsync(CancellationToken.None), o.InvoiceExportCron, tz);
    RecurringJob.AddOrUpdate<DocumentPurgeJob>("document-purge", j => j.RunAsync(CancellationToken.None), o.DocumentPurgeCron, tz);
    RecurringJob.AddOrUpdate<PositionRetentionJob>("position-retention", j => j.RunAsync(CancellationToken.None), o.PositionRetentionCron, tz);
    RecurringJob.AddOrUpdate<FuelSurchargeImportJob>("fuel-surcharge-import", j => j.RunAsync(CancellationToken.None), o.FuelSurchargeCron, tz);
}
