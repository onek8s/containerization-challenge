using CityPermits.Data;
using Quartz;

namespace CityPermits.Jobs.Jobs;

[DisallowConcurrentExecution]
public abstract class JobBase : IJob
{
    protected readonly PermitsDbContext Db;
    protected readonly ILogger Log;

    protected JobBase(PermitsDbContext db, ILogger log) { Db = db; Log = log; }

    public async Task Execute(IJobExecutionContext context)
    {
        var run = new JobRun { Job = context.JobDetail.Key.Name, StartedUtc = DateTime.UtcNow, Host = Environment.MachineName };
        try
        {
            run.Items = await RunAsync(context.CancellationToken);
            run.Success = true;
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "Job {Job} failed", run.Job);
            run.Message = ex.Message;
        }
        run.FinishedUtc = DateTime.UtcNow;
        Db.ChangeTracker.Clear();
        Db.JobRuns.Add(run);
        await Db.SaveChangesAsync();
        Log.LogInformation("Job {Job} finished: {Items} items, success {Success}", run.Job, run.Items, run.Success);
    }

    protected abstract Task<int> RunAsync(CancellationToken ct);
}
