using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Humongous.Quoting.Rating;

public sealed record RatingEngineState(
    string Provider,
    string EngineVersion,
    int Workers,
    int Busy,
    int Queued,
    long Calls,
    string? LastError);

public interface IRatingEngineStatus
{
    RatingEngineState GetState();
}

public sealed class RatingEngineHealthCheck(IRatingEngineStatus status) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var state = status.GetState();
        var data = new Dictionary<string, object>
        {
            ["provider"] = state.Provider,
            ["engineVersion"] = state.EngineVersion,
            ["workers"] = state.Workers,
            ["busy"] = state.Busy,
            ["queued"] = state.Queued,
            ["calls"] = state.Calls
        };
        if (state.LastError is not null) data["lastError"] = state.LastError;

        var result = state.Queued > state.Workers * 10
            ? HealthCheckResult.Degraded("Rating queue is backing up.", data: data)
            : HealthCheckResult.Healthy(data: data);
        return Task.FromResult(result);
    }
}
