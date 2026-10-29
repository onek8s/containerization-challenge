using Lucerne.Lending.Data;

namespace Lucerne.Lending.Collections.Jobs;

public interface ICollectionsJob
{
    string Name { get; }
    Task<string> RunAsync(CancellationToken ct);
}

/// <summary>Runs a job with a JobRun record; FluentScheduler calls it on its own threads.</summary>
public sealed class JobRunner
{
    private readonly IEnumerable<ICollectionsJob> _jobs;
    private readonly JobRunRepository _runs;
    private readonly ILogger<JobRunner> _log;
    private readonly Dictionary<string, SemaphoreSlim> _running = new();

    public JobRunner(IEnumerable<ICollectionsJob> jobs, JobRunRepository runs, ILogger<JobRunner> log)
    {
        _jobs = jobs;
        _runs = runs;
        _log = log;
        foreach (var j in jobs) _running[j.Name] = new SemaphoreSlim(1, 1);
    }

    public IEnumerable<string> Names => _jobs.Select(j => j.Name);

    public async Task<bool> RunAsync(string name, CancellationToken ct = default)
    {
        var job = _jobs.FirstOrDefault(j => string.Equals(j.Name, name, StringComparison.OrdinalIgnoreCase))
                  ?? throw new ArgumentException($"Unknown job {name}. Jobs: {string.Join(", ", Names)}");
        var gate = _running[job.Name];
        if (!await gate.WaitAsync(0, ct))
        {
            _log.LogWarning("Job {Job} is still running, skipped", job.Name);
            return false;
        }
        var id = await _runs.StartAsync(job.Name);
        try
        {
            var detail = await job.RunAsync(ct);
            await _runs.FinishAsync(id, "Succeeded", detail);
            _log.LogInformation("Job {Job}: {Detail}", job.Name, detail);
            return true;
        }
        catch (Exception ex)
        {
            await _runs.FinishAsync(id, "Failed", ex.Message);
            _log.LogError(ex, "Job {Job} failed", job.Name);
            return false;
        }
        finally
        {
            gate.Release();
        }
    }
}
