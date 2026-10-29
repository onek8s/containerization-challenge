using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductReturns.Core;
using ProductReturns.Data;

namespace ProductReturns.Api.Services
{
    public class CatalogCacheOptions
    {
        /// <summary>Folder for the catalog snapshot used when the ERP replica is not available at start-up.</summary>
        public string Folder { get; set; } = @"D:\ReturnsData\Api\Cache";

        public int RefreshMinutes { get; set; } = 360;
    }

    /// <summary>
    /// Item master from the ERP replica, kept in memory. A snapshot is written to disk after each refresh.
    /// </summary>
    public class CatalogCache
    {
        private const string SnapshotName = "catalog.json";

        private readonly IServiceScopeFactory _scopes;
        private readonly CatalogCacheOptions _options;
        private readonly ILogger<CatalogCache> _logger;
        private readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);
        private ConcurrentDictionary<string, CatalogItem> _items = new ConcurrentDictionary<string, CatalogItem>(StringComparer.OrdinalIgnoreCase);

        public CatalogCache(IServiceScopeFactory scopes, IOptions<CatalogCacheOptions> options, ILogger<CatalogCache> logger)
        {
            _scopes = scopes;
            _options = options.Value;
            _logger = logger;
        }

        public DateTime? LoadedUtc { get; private set; }

        public int Count => _items.Count;

        public CatalogItem Find(string sku)
        {
            if (sku == null)
            {
                return null;
            }

            _items.TryGetValue(sku, out var item);
            return item;
        }

        public async Task RefreshAsync(CancellationToken cancellationToken)
        {
            await _refreshLock.WaitAsync(cancellationToken);
            try
            {
                List<CatalogItem> items;
                using (var scope = _scopes.CreateScope())
                {
                    items = await scope.ServiceProvider.GetRequiredService<ReturnsRepository>().LoadCatalogAsync(cancellationToken);
                }

                Replace(items);
                LoadedUtc = DateTime.UtcNow;
                _logger.LogInformation("Catalog loaded from the ERP replica: {Count} items", items.Count);
                WriteSnapshot(items);
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        public bool LoadSnapshot()
        {
            var path = Path.Combine(_options.Folder, SnapshotName);
            if (!File.Exists(path))
            {
                return false;
            }

            var items = JsonSerializer.Deserialize<List<CatalogItem>>(File.ReadAllText(path));
            Replace(items);
            LoadedUtc = File.GetLastWriteTimeUtc(path);
            _logger.LogWarning("Catalog loaded from snapshot {Path} written {Written:u}: {Count} items", path, LoadedUtc, items.Count);
            return true;
        }

        private void Replace(IEnumerable<CatalogItem> items)
        {
            var map = new ConcurrentDictionary<string, CatalogItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items.Where(i => !string.IsNullOrEmpty(i.Sku)))
            {
                map[item.Sku.Trim()] = item;
            }

            _items = map;
        }

        private void WriteSnapshot(List<CatalogItem> items)
        {
            try
            {
                Directory.CreateDirectory(_options.Folder);
                var path = Path.Combine(_options.Folder, SnapshotName);
                var temp = path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(items));
                if (File.Exists(path))
                {
                    File.Replace(temp, path, null);
                }
                else
                {
                    File.Move(temp, path);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Could not write the catalog snapshot to {Folder}", _options.Folder);
            }
        }
    }

    public class CatalogRefreshService : BackgroundService
    {
        private readonly CatalogCache _cache;
        private readonly CatalogCacheOptions _options;
        private readonly ILogger<CatalogRefreshService> _logger;

        public CatalogRefreshService(CatalogCache cache, IOptions<CatalogCacheOptions> options, ILogger<CatalogRefreshService> logger)
        {
            _cache = cache;
            _options = options.Value;
            _logger = logger;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            // Load before the first request is served.
            try
            {
                await _cache.RefreshAsync(cancellationToken);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logger.LogError(ex, "Catalog refresh failed at start-up");
                if (!_cache.LoadSnapshot())
                {
                    throw;
                }
            }

            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromMinutes(Math.Max(5, _options.RefreshMinutes));
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(interval, stoppingToken);
                try
                {
                    await _cache.RefreshAsync(stoppingToken);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _logger.LogError(ex, "Catalog refresh failed, keeping {Count} cached items", _cache.Count);
                }
            }
        }
    }
}
