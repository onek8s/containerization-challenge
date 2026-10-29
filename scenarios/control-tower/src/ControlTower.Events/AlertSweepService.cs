using Microsoft.Extensions.Options;

namespace Proseware.ControlTower.Events;

/// <summary>Evaluates the alert rules for all open shipments on a fixed interval.</summary>
public sealed class AlertSweepService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly EventsOptions _options;
    private readonly ILogger<AlertSweepService> _logger;

    public AlertSweepService(IServiceScopeFactory scopes, IOptions<EventsOptions> options, ILogger<AlertSweepService> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // give the subscriptions a moment to start first
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.RuleSweepMinutes));
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var reconciler = scope.ServiceProvider.GetRequiredService<AlertReconciler>();
                var started = DateTime.UtcNow;
                var (raised, closed) = await reconciler.RunAsync(null, _options.ScanLookbackHours, stoppingToken);
                _logger.LogInformation("Rule sweep done in {Ms} ms: {Raised} raised, {Closed} closed", (int)(DateTime.UtcNow - started).TotalMilliseconds, raised, closed);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Rule sweep failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
