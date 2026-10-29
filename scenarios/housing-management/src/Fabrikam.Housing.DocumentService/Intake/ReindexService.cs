using System;
using System.Threading;
using System.Threading.Tasks;
using Fabrikam.Housing.DocumentService.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fabrikam.Housing.DocumentService.Intake
{
    /// <summary>Every night at Storage:ReindexHour, indexes documents that are not in the full text index yet.</summary>
    public class ReindexService : BackgroundService
    {
        private readonly DocumentStore _store;
        private readonly StorageOptions _options;
        private readonly ILogger<ReindexService> _logger;

        public ReindexService(DocumentStore store, StorageOptions options, ILogger<ReindexService> logger)
        {
            _store = store;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();
            // Catch up once at start-up (e.g. after the index folder was rebuilt).
            await RunAsync();
            while (!stoppingToken.IsCancellationRequested)
            {
                var next = DateTime.Today.AddHours(_options.ReindexHour);
                if (next <= DateTime.Now) next = next.AddDays(1);
                await Task.Delay(next - DateTime.Now, stoppingToken);
                await RunAsync();
            }
        }

        private async Task RunAsync()
        {
            try
            {
                var count = await _store.ReindexPendingAsync();
                _logger.LogInformation("Re-index finished: {Count} documents added to the index", count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Re-index failed");
            }
        }
    }
}
