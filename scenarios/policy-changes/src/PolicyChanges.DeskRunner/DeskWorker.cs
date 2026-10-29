using Microsoft.Extensions.Options;
using PolicyChanges.Core.Flows;
using PolicyChanges.Data.Entities;
using PolicyChanges.Data.Services;
using PolicyChanges.DeskRunner.Desktop;

namespace PolicyChanges.DeskRunner;

/// <summary>Takes changes from the queue and enters them in PolicyDesk, one at a time.</summary>
internal sealed class DeskWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly PolicyDeskClient _client;
    private readonly FlowLibrary _flows;
    private readonly EvidenceWriter _evidence;
    private readonly DeskOptions _options;
    private readonly ILogger<DeskWorker> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _station;
    private readonly string _user = $@"{Environment.UserDomainName}\{Environment.UserName}";

    public DeskWorker(IServiceScopeFactory scopes, PolicyDeskClient client, FlowLibrary flows, EvidenceWriter evidence,
        IOptions<DeskOptions> options, ILogger<DeskWorker> logger, ILoggerFactory loggerFactory)
    {
        _scopes = scopes;
        _client = client;
        _flows = flows;
        _evidence = evidence;
        _options = options.Value;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _station = string.IsNullOrWhiteSpace(_options.StationName) ? Environment.MachineName : _options.StationName;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Desk station {Station} started as {User} in session {Session}; {Count} flows from {Folder}",
            _station, _user, InteractiveSession.CurrentSessionId, _flows.All.Count, _flows.Folder);
        InteractiveSession.KeepAwake(true);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = await RunOnceAsync(stoppingToken);
                await Task.Delay(delay, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            InteractiveSession.KeepAwake(false);
            await HeartbeatAsync("Stopped", null, null, null, CancellationToken.None);
        }
    }

    private async Task<TimeSpan> RunOnceAsync(CancellationToken ct)
    {
        var poll = TimeSpan.FromSeconds(_options.PollSeconds);
        var desktopWait = TimeSpan.FromSeconds(_options.DesktopWaitSeconds);

        var session = InteractiveSession.Check();
        if (!session.Usable)
        {
            _logger.LogWarning("Desktop not usable: {Reason}", session.Reason);
            await HeartbeatAsync("Waiting for desktop", session.Reason, null, null, ct);
            return desktopWait;
        }

        try
        {
            await OnDeskThread(_client.EnsureReady);
        }
        catch (DesktopException ex)
        {
            _logger.LogError(ex, "PolicyDesk is not ready");
            await HeartbeatAsync("PolicyDesk error", ex.Message, null, null, ct);
            _client.Stop();
            return desktopWait;
        }

        using var scope = _scopes.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<RequestQueue>();
        var request = await queue.ClaimNextAsync(_station, ct);
        if (request is null)
        {
            await HeartbeatAsync("Idle", session.Reason, null, null, ct);
            return poll;
        }

        await HeartbeatAsync("Working", $"{request.ChangeType} on {request.PolicyNumber}", request.Reference, null, ct);
        var result = await EnterAsync(request, ct);

        if (result.Outcome == FlowOutcome.Failed && !InteractiveSession.Check().Usable)
        {
            // The desktop went away during the flow (locked, disconnected); not the change's fault.
            await queue.ReleaseAsync(request.Id, _station, "desktop became unavailable", ct);
            await HeartbeatAsync("Waiting for desktop", result.Message, null, null, ct);
            return desktopWait;
        }

        var files = _evidence.Save(request.Reference, request.CreatedUtc, request.Attempts, result.Evidence);
        await queue.CompleteAsync(request.Id, _station, result.Outcome, result.Message, result.Endorsement, files, ct);
        _logger.LogInformation("{Reference} {ChangeType} {Policy}: {Outcome} {Message}",
            request.Reference, request.ChangeType, request.PolicyNumber, result.Outcome, result.Message);

        if (result.Outcome == FlowOutcome.Failed) _client.Stop();
        else _client.ChangeEntered();

        await HeartbeatAsync("Idle", null, null, result.Outcome, ct);
        return TimeSpan.Zero;
    }

    private async Task<FlowResult> EnterAsync(ChangeRequest request, CancellationToken ct)
    {
        var flow = _flows.For(request.ChangeType);
        if (flow is null)
            return new FlowResult(FlowOutcome.Failed, $"No flow for {request.ChangeType}.", new Dictionary<string, string>(), Array.Empty<FlowEvidence>());

        if (!string.IsNullOrEmpty(flow.ClientVersion) && _client.Version is { } v && !v.StartsWith(flow.ClientVersion, StringComparison.Ordinal))
            _logger.LogWarning("Flow {Flow} was recorded for PolicyDesk {Expected}, installed is {Actual}", flow.Name, flow.ClientVersion, v);

        using var leaseCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var lease = RenewLeaseAsync(request.Id, leaseCts.Token);
        try
        {
            var runner = new FlowRunner(_client.Desktop, _loggerFactory.CreateLogger<FlowRunner>());
            return await OnDeskThread(() => runner.Run(flow, request.ToFlowRequest()));
        }
        finally
        {
            leaseCts.Cancel();
            try { await lease; } catch (OperationCanceledException) { }
        }
    }

    private async Task RenewLeaseAsync(long id, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<QueueOptions>>().Value;
        var queue = scope.ServiceProvider.GetRequiredService<RequestQueue>();
        var interval = TimeSpan.FromMinutes(Math.Max(1, options.LeaseMinutes / 2.0));
        while (true)
        {
            await Task.Delay(interval, ct);
            if (!await queue.RenewAsync(id, _station, ct))
                _logger.LogWarning("Lease for request {Id} could not be renewed", id);
        }
    }

    private async Task HeartbeatAsync(string state, string? detail, string? reference, FlowOutcome? outcome, CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var queue = scope.ServiceProvider.GetRequiredService<RequestQueue>();
            await queue.HeartbeatAsync(_station, _user, state, detail, _client.Version, reference, outcome, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Heartbeat failed");
        }
    }

    /// <summary>All PolicyDesk work runs on one dedicated thread: input queue attachment and focus are per thread.</summary>
    private static Task<T> OnDeskThread<T>(Func<T> work) => DeskThread.Instance.Run(work);

    private static Task OnDeskThread(Action work) => DeskThread.Instance.Run(() => { work(); return true; });
}

internal sealed class DeskThread
{
    public static readonly DeskThread Instance = new();

    private readonly System.Collections.Concurrent.BlockingCollection<Action> _work = new();

    private DeskThread()
    {
        var thread = new Thread(() =>
        {
            foreach (var item in _work.GetConsumingEnumerable()) item();
        })
        {
            IsBackground = true,
            Name = "PolicyDesk",
        };
        thread.Start();
    }

    public Task<T> Run<T>(Func<T> work)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _work.Add(() =>
        {
            try { tcs.SetResult(work()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }
}
