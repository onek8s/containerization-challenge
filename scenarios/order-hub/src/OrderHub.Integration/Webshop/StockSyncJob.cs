using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Litware.OrderHub.Core;
using Litware.OrderHub.Core.Warehouse;
using Litware.OrderHub.Integration.Sftp;
using Serilog;

namespace Litware.OrderHub.Integration.Webshop
{
    /// <summary>
    /// Takes the newest warehouse stock snapshot, updates webshop stock quantities and uploads the
    /// marketplace offer stock file. Older snapshots are archived without processing.
    /// </summary>
    public class StockSyncJob
    {
        private readonly ISftpConnector _warehouse;
        private readonly string _stockDirectory;
        private readonly WebshopApiClient _webshop;
        private readonly ISftpConnector _marketplace;
        private readonly string _marketplaceStockDirectory;
        private readonly int _safetyStock;
        private IDictionary<string, WebshopProductRef> _productCache = new Dictionary<string, WebshopProductRef>();
        private DateTime _cacheLoadedUtc = DateTime.MinValue;
        private readonly Dictionary<string, int> _lastSent = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public StockSyncJob(ISftpConnector warehouse, string stockDirectory, WebshopApiClient webshop,
            ISftpConnector marketplace, string marketplaceStockDirectory, int safetyStock)
        {
            _warehouse = warehouse;
            _stockDirectory = stockDirectory.TrimEnd('/');
            _webshop = webshop;
            _marketplace = marketplace;
            _marketplaceStockDirectory = marketplaceStockDirectory;
            _safetyStock = safetyStock;
        }

        public async Task<int> RunAsync(CancellationToken ct)
        {
            IReadOnlyList<StockLevel> levels;
            string newest;
            using (var session = Resilience.Sftp("Stock snapshot").Execute(() => _warehouse.Connect()))
            {
                var files = session.ListFiles(_stockDirectory, ".csv")
                    .Where(f => StockReportFile.SnapshotTime(f) != null)
                    .OrderBy(f => StockReportFile.SnapshotTime(f))
                    .ToList();
                if (files.Count == 0) return 0;

                newest = files.Last();
                foreach (var old in files.Take(files.Count - 1))
                {
                    session.Move(old, _stockDirectory + "/archive");
                }

                levels = StockReportFile.Read(new StringReader(Encoding.UTF8.GetString(session.Download(newest))));
                session.Move(newest, _stockDirectory + "/archive");
            }

            var publishable = levels.ToDictionary(l => l.Sku, l => Math.Max(0, l.Available - _safetyStock), StringComparer.OrdinalIgnoreCase);
            var changed = publishable.Where(p => !_lastSent.TryGetValue(p.Key, out var last) || last != p.Value).ToList();

            if (DateTime.UtcNow - _cacheLoadedUtc > TimeSpan.FromHours(6) || changed.Any(c => !_productCache.ContainsKey(c.Key)))
            {
                _productCache = await _webshop.FindProductsAsync(publishable.Keys, ct);
                _cacheLoadedUtc = DateTime.UtcNow;
            }

            var updates = changed.Where(c => _productCache.ContainsKey(c.Key))
                .Select(c => new KeyValuePair<WebshopProductRef, int>(_productCache[c.Key], c.Value)).ToList();
            int updated = await _webshop.UpdateStockAsync(updates, ct);
            foreach (var c in changed) _lastSent[c.Key] = c.Value;

            if (_marketplace != null)
            {
                var sb = new StringBuilder("Sku;Stock\r\n");
                foreach (var p in publishable.OrderBy(p => p.Key)) sb.Append(p.Key).Append(';').Append(p.Value.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
                byte[] offers = Encoding.UTF8.GetBytes(sb.ToString());
                Resilience.Sftp("Marketplace stock").Execute(() =>
                {
                    using (var session = _marketplace.Connect())
                    {
                        session.Upload(_marketplaceStockDirectory, "offers_stock.csv", offers);
                    }
                });
            }

            var unknown = changed.Where(c => !_productCache.ContainsKey(c.Key)).Select(c => c.Key).ToList();
            if (unknown.Count > 0)
            {
                Log.Warning("Stock for {Count} SKUs not in the webshop: {Skus}", unknown.Count, string.Join(", ", unknown.Take(20)));
            }
            Log.Information("Stock snapshot {File}: {Skus} SKUs, {Updated} webshop updates", newest, levels.Count, updated);
            return updated;
        }
    }
}
