using Quartz;
using Serilog;
using StudyData.Calculation;
using StudyData.Coding;
using StudyData.Data;
using StudyData.Documents;
using StudyData.Integration;
using StudyData.Jobs;
using StudyData.Jobs.Jobs;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(o => o.ServiceName = "StudyDataJobs");
builder.Services.AddSerilog((services, log) =>
{
    log.ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(builder.Configuration["Logging:LogFile"] ?? "logs/jobs-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 90);
    if (!builder.Environment.IsDevelopment())
    {
        log.WriteTo.EventLog("StudyData Jobs", manageEventSource: false, restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning);
    }
});

var options = builder.Configuration.GetSection(JobsOptions.Section).Get<JobsOptions>() ?? new JobsOptions();
builder.Services.Configure<JobsOptions>(builder.Configuration.GetSection(JobsOptions.Section));
builder.Services.AddStudyData(builder.Configuration);
builder.Services.AddStudyCalculation(builder.Configuration);
builder.Services.AddStudyDocuments(builder.Configuration);
builder.Services.AddStudyCoding(builder.Configuration);
builder.Services.AddStudyIntegration(builder.Configuration);
builder.Services.AddScoped<SafetyReportBuilder>();

builder.Services.AddQuartz(q =>
{
    void Schedule<T>(string cron) where T : IJob
    {
        var key = new JobKey(typeof(T).Name);
        q.AddJob<T>(key);
        q.AddTrigger(t => t.ForJob(key).WithIdentity(key.Name + ".trigger").WithCronSchedule(cron, c => c.InTimeZone(TimeZoneInfo.Local)));
    }

    Schedule<GeneratedDocumentsJob>(options.DocumentsCron);
    Schedule<NightlyCasebooksJob>(options.NightlyCasebooksCron);
    Schedule<SafetyReportJob>(options.SafetyReportCron);
    Schedule<CodingJob>(options.CodingCron);
    Schedule<LabImportJob>(options.LabImportCron);
    Schedule<QueryRemindersJob>(options.QueryRemindersCron);
});
builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

var host = builder.Build();
if (builder.Environment.IsDevelopment())
{
    StudyData.Data.ServiceCollectionExtensions.EnsureDataDirectory(builder.Configuration.GetConnectionString("StudyData")!);
    await StudyData.Data.Seed.DbInitializer.EnsureCreatedAsync(host.Services, seedSampleData: false);
}

if (args.Length > 0 && args[0] == "run")
{
    // StudyData.Jobs.exe run <JobName>: runs one job now, for operators and after a restore.
    await JobRunner.RunOnceAsync(host.Services, args.Skip(1).FirstOrDefault());
    return;
}

host.Run();
