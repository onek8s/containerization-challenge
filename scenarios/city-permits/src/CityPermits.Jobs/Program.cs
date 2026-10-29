using CityPermits.Archive;
using CityPermits.Data;
using CityPermits.Documents;
using CityPermits.Gis;
using CityPermits.Integration;
using CityPermits.Jobs.Jobs;
using Quartz;
using Serilog;

// CityPermits.Jobs.exe            run as the CityPermitsJobs Windows service (or console)
// CityPermits.Jobs.exe run <job>  run one job now and exit
var builder = Host.CreateApplicationBuilder(args.Where(a => a.Contains('=')).ToArray());
builder.Services.AddWindowsService(o => o.ServiceName = "CityPermitsJobs");
builder.Services.AddSerilog((sp, cfg) => cfg.ReadFrom.Configuration(builder.Configuration));
var cfg = builder.Configuration;

builder.Services.AddPermitsData(cfg);
builder.Services.AddCityGis(cfg);
builder.Services.AddDocumentArchive(cfg);
builder.Services.AddSingleton(new PermitDocuments());
builder.Services.AddSingleton(cfg.GetSection("Mail").Get<MailOptions>() ?? new());
builder.Services.AddSingleton<Mailer>();
builder.Services.AddSingleton(cfg.GetSection("Records").Get<RecordsOptions>() ?? new());
builder.Services.AddSingleton(sp => cfg["Records:Mode"] == "Simulator"
    ? new RecordsSimulator() as IRecordCaptureService
    : RecordsClientFactory.Create(sp.GetRequiredService<RecordsOptions>()));
builder.Services.AddSingleton(cfg.GetSection("Paystream").Get<PaystreamOptions>() ?? new());

var jobs = new (string Name, Type Type, string Cron)[]
{
    ("outbox", typeof(OutboxJob), cfg["Schedules:Outbox"] ?? "0/30 * * * * ?"),
    ("records-filing", typeof(RecordsFilingJob), cfg["Schedules:RecordsFiling"] ?? "0 0/10 7-19 ? * MON-FRI"),
    ("renewals", typeof(RenewalJob), cfg["Schedules:Renewals"] ?? "0 0 6 * * ?"),
    ("expiry", typeof(ExpiryJob), cfg["Schedules:Expiry"] ?? "0 15 0 * * ?"),
    ("gis-replica", typeof(GisReplicaJob), cfg["Schedules:GisReplica"] ?? "0 30 2 * * ?"),
    ("settlement", typeof(SettlementJob), cfg["Schedules:Settlement"] ?? "0 0 8 ? * MON-FRI"),
    ("consultation-reminders", typeof(ConsultationReminderJob), cfg["Schedules:ConsultationReminders"] ?? "0 0 7 ? * MON-FRI"),
};

builder.Services.AddQuartz(q =>
{
    // RAM store: one jobs server; the GIS replica job needs the local Geomatix engine anyway.
    q.SchedulerName = "CityPermitsJobs";
    foreach (var (name, type, cron) in jobs)
    {
        var key = new JobKey(name);
        q.AddJob(type, key, j => j.StoreDurably());
        if (args.FirstOrDefault() != "run")
            q.AddTrigger(t => t.ForJob(key).WithIdentity(name + "-trigger")
                .WithCronSchedule(cron, c => c.InTimeZone(TimeZoneInfo.FindSystemTimeZoneById(cfg["Schedules:TimeZone"] ?? "GMT Standard Time"))));
    }
});
builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

var host = builder.Build();
if (args.FirstOrDefault() == "run" && args.Length > 1)
{
    await host.StartAsync();
    var scheduler = await host.Services.GetRequiredService<ISchedulerFactory>().GetScheduler();
    var done = new TaskCompletionSource();
    scheduler.ListenerManager.AddJobListener(new CompletionListener(done));
    await scheduler.TriggerJob(new JobKey(args[1]));
    await done.Task.WaitAsync(TimeSpan.FromMinutes(30));
    await host.StopAsync();
    return;
}
host.Run();

sealed class CompletionListener : IJobListener
{
    private readonly TaskCompletionSource _done;
    public CompletionListener(TaskCompletionSource done) => _done = done;
    public string Name => "completion";
    public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken ct = default) => Task.CompletedTask;
    public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken ct = default) => Task.CompletedTask;
    public Task JobWasExecuted(IJobExecutionContext context, JobExecutionException? e, CancellationToken ct = default) { _done.TrySetResult(); return Task.CompletedTask; }
}
