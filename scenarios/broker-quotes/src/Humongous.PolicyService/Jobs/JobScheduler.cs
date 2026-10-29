using Cronos;
using Microsoft.Extensions.Options;

namespace Humongous.PolicyService.Jobs;

public interface IScheduledJob
{
    string Name { get; }

    Task RunAsync(CancellationToken ct);
}

public sealed class JobOptions
{
    public const string SectionName = "Jobs";

    /// <summary>Cron expressions (local time of the server) by job name. Empty disables a job.</summary>
    public Dictionary<string, string> Schedules { get; set; } = [];

    public string TimeZone { get; set; } = "GMT Standard Time";
}

/// <summary>Runs the jobs on their cron schedules. One run of a job at a time.</summary>
public sealed class JobScheduler(
    IServiceScopeFactory scopes,
    IOptions<JobOptions> options,
    TimeProvider clock,
    ILogger<JobScheduler> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
        var loops = new List<Task>();
        foreach (var (name, expression) in options.Value.Schedules)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                logger.LogInformation("Job {Job} is disabled", name);
                continue;
            }
            loops.Add(LoopAsync(name, CronExpression.Parse(expression), zone, stoppingToken));
        }
        return Task.WhenAll(loops);
    }

    private async Task LoopAsync(string name, CronExpression cron, TimeZoneInfo zone, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var now = clock.GetUtcNow();
            var next = cron.GetNextOccurrence(now, zone);
            if (next is null) return;
            logger.LogInformation("Job {Job} next runs at {Next:yyyy-MM-dd HH:mm} UTC", name, next.Value.UtcDateTime);
            await Task.Delay(next.Value - now, clock, ct);
            await RunJobAsync(scopes, name, logger, ct);
        }
    }

    public static async Task<bool> RunJobAsync(IServiceScopeFactory scopes, string name, ILogger logger, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var job = scope.ServiceProvider.GetServices<IScheduledJob>()
            .FirstOrDefault(j => string.Equals(j.Name, name, StringComparison.OrdinalIgnoreCase));
        if (job is null)
        {
            logger.LogError("No job named {Job}", name);
            return false;
        }

        var started = DateTime.UtcNow;
        try
        {
            await job.RunAsync(ct);
            logger.LogInformation("Job {Job} finished in {Seconds:0.0} s", name, (DateTime.UtcNow - started).TotalSeconds);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Job {Job} failed", name);
            return false;
        }
    }
}
