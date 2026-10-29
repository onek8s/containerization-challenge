using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StudyData.Data;
using StudyData.Monitor.Api.Services;

namespace StudyData.Monitor.Api.Hubs;

[Authorize(Policy = Policies.Review)]
public class NotificationHub : Hub
{
    public Task Follow(int studyId) => Groups.AddToGroupAsync(Context.ConnectionId, $"study-{studyId}");

    public Task Unfollow(int studyId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"study-{studyId}");
}

/// <summary>
/// Delivers notifications written by the site portal, the jobs service and this application to the
/// connected monitors. Notifications are rows in the database; this relay marks them delivered.
/// </summary>
public class NotificationRelay : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<NotificationRelay> _log;
    private readonly TimeSpan _interval;

    public NotificationRelay(IServiceScopeFactory scopes, IHubContext<NotificationHub> hub, Microsoft.Extensions.Options.IOptions<MonitorOptions> options,
        ILogger<NotificationRelay> log)
    {
        _scopes = scopes;
        _hub = hub;
        _log = log;
        _interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.NotificationPollSeconds));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<StudyDataContext>();
                var batch = await db.Notifications.Where(n => !n.Delivered).OrderBy(n => n.Id).Take(100).ToListAsync(stoppingToken);
                foreach (var n in batch)
                {
                    await _hub.Clients.Group($"study-{n.StudyId}").SendAsync("notification",
                        new { n.Id, n.At, n.Kind, n.Text, n.SubjectId, n.SiteId }, stoppingToken);
                    n.Delivered = true;
                }

                if (batch.Count > 0)
                {
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "Notification relay failed");
            }
        }
    }
}
