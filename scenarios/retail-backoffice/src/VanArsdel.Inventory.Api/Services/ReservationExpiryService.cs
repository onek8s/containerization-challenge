using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VanArsdel.BuildingBlocks.Caching;

namespace VanArsdel.Inventory.Api.Services
{
    /// <summary>Releases reservations of orders that were never picked (every 10 minutes, one instance).</summary>
    public class ReservationExpiryService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IDistributedLockProvider _locks;
        private readonly ILogger<ReservationExpiryService> _logger;

        public ReservationExpiryService(IServiceScopeFactory scopes, IDistributedLockProvider locks, ILogger<ReservationExpiryService> logger)
        {
            _scopes = scopes;
            _locks = locks;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                    await using var handle = await _locks.TryAcquireAsync("inventory:reservation-expiry", TimeSpan.FromMinutes(5), TimeSpan.Zero, stoppingToken);
                    if (handle == null) continue;
                    using var scope = _scopes.CreateScope();
                    var released = await scope.ServiceProvider.GetRequiredService<StockService>().ExpireReservationsAsync(stoppingToken);
                    if (released > 0) _logger.LogInformation("Released {Count} expired reservations", released);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Reservation expiry failed");
                }
            }
        }
    }
}
