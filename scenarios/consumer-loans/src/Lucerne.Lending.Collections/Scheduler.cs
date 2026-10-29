using FluentScheduler;
using Lucerne.Lending.Collections.Jobs;

namespace Lucerne.Lending.Collections;

/// <summary>Schedules the collections jobs with FluentScheduler (times are server local time).</summary>
public sealed class Scheduler : IHostedService
{
    private readonly JobRunner _runner;
    private readonly IConfiguration _configuration;
    private readonly ILogger<Scheduler> _log;

    public Scheduler(JobRunner runner, IConfiguration configuration, ILogger<Scheduler> log)
    {
        _runner = runner;
        _configuration = configuration;
        _log = log;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var registry = new Registry();
        registry.NonReentrantAsDefault();
        Schedule(registry, "payment-plans", s => s.ToRunEvery(1).Weekdays().At(6, 45));
        Schedule(registry, "daily-stages", s => s.ToRunEvery(1).Weekdays().At(7, 30));
        Schedule(registry, "gl-export", s => s.ToRunEvery(1).Days().At(21, 0));
        Schedule(registry, "agency-handover", s => s.ToRunEvery(1).Weeks().On(DayOfWeek.Monday).At(8, 0));
        JobManager.JobException += info => _log.LogError(info.Exception, "Scheduled job {Job} failed", info.Name);
        JobManager.Initialize(registry);
        _log.LogInformation("Collections jobs scheduled: {Jobs}", string.Join(", ", JobManager.AllSchedules.Select(s => $"{s.Name} next {s.NextRun:g}")));
        return Task.CompletedTask;
    }

    private void Schedule(Registry registry, string job, Action<Schedule> when)
    {
        if (!_configuration.GetValue($"Collections:Jobs:{job}:Enabled", true)) return;
        var schedule = registry.Schedule(() => _runner.RunAsync(job).GetAwaiter().GetResult()).WithName(job);
        when(schedule);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        JobManager.StopAndBlock();
        return Task.CompletedTask;
    }
}
