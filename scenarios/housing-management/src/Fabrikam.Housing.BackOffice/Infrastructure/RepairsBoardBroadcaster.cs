using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fabrikam.Housing.BackOffice.Hubs;
using Fabrikam.Housing.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fabrikam.Housing.BackOffice.Infrastructure
{
    /// <summary>
    /// Polls work order events every 15 seconds (the contractor API and the jobs host write them too)
    /// and pushes new ones to the repairs board.
    /// </summary>
    public class RepairsBoardBroadcaster : BackgroundService
    {
        private static int _lastEventId = -1;
        private readonly IServiceScopeFactory _scopes;
        private readonly IHubContext<RepairsHub> _hub;
        private readonly ILogger<RepairsBoardBroadcaster> _logger;

        public RepairsBoardBroadcaster(IServiceScopeFactory scopes, IHubContext<RepairsHub> hub, ILogger<RepairsBoardBroadcaster> logger)
        {
            _scopes = scopes;
            _hub = hub;
            _logger = logger;
        }

        public static int LastEventId => _lastEventId;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Give the application time to start (and the development database to be seeded).
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PollAsync(stoppingToken);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _logger.LogWarning(ex, "Repairs board poll failed");
                }
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }

        private async Task PollAsync(CancellationToken token)
        {
            using (var scope = _scopes.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<HousingDbContext>();
                if (_lastEventId < 0)
                {
                    _lastEventId = await db.WorkOrderEvents.Select(e => (int?)e.Id).MaxAsync(token) ?? 0;
                    return;
                }

                var events = await db.WorkOrderEvents.AsNoTracking()
                    .Where(e => e.Id > _lastEventId)
                    .OrderBy(e => e.Id)
                    .Take(200)
                    .ToListAsync(token);
                if (events.Count == 0) return;

                var ids = events.Select(e => e.WorkOrderId).Distinct().ToList();
                var orders = await db.WorkOrders.AsNoTracking()
                    .Include(w => w.Property)
                    .Include(w => w.Contractor)
                    .Where(w => ids.Contains(w.Id))
                    .ToDictionaryAsync(w => w.Id, token);

                var now = DateTime.Now;
                foreach (var e in events)
                {
                    if (!orders.TryGetValue(e.WorkOrderId, out var order)) continue;
                    var update = new BoardUpdate
                    {
                        EventId = e.Id,
                        WorkOrderId = order.Id,
                        Reference = order.Reference,
                        Patch = order.Property.PatchCode,
                        Address = order.Property.AddressLine1,
                        Priority = order.Priority.ToString(),
                        Status = order.Status.ToString(),
                        Contractor = order.Contractor?.Code,
                        Actor = e.Actor,
                        Text = e.Text,
                        At = e.At.ToString("dd/MM HH:mm"),
                        Overdue = order.IsOpen && order.TargetDate < now
                    };
                    await _hub.Clients.Group(RepairsHub.AllPatches).SendAsync("workOrderChanged", update, token);
                    if (!string.IsNullOrEmpty(update.Patch))
                        await _hub.Clients.Group(update.Patch).SendAsync("workOrderChanged", update, token);
                }
                _lastEventId = events.Last().Id;
                _logger.LogDebug("Pushed {Count} repairs board updates", events.Count);
            }
        }
    }
}
