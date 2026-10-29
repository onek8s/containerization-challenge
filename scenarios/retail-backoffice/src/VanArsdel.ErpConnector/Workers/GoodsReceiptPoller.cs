using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VanArsdel.Contracts.Inventory;
using VanArsdel.Integration.Erp;

namespace VanArsdel.ErpConnector.Workers
{
    /// <summary>The ERP cannot call out, so we poll for posted goods receipts and acknowledge them once published.</summary>
    public class GoodsReceiptPoller : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IBus _bus;
        private readonly ErpConnectorOptions _options;
        private readonly ILogger<GoodsReceiptPoller> _logger;

        public GoodsReceiptPoller(IServiceScopeFactory scopes, IBus bus, IOptions<ErpConnectorOptions> options, ILogger<GoodsReceiptPoller> logger)
        {
            _scopes = scopes;
            _bus = bus;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_options.ReceiptPollSeconds <= 0)
            {
                _logger.LogInformation("Goods receipt polling is off");
                return;
            }
            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var site in _options.ReceiptSites)
                {
                    try
                    {
                        await PollSiteAsync(site, stoppingToken);
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogWarning(ex, "Goods receipt poll for {Site} failed", site);
                    }
                }
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.ReceiptPollSeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task PollSiteAsync(string site, CancellationToken cancellationToken)
        {
            using var scope = _scopes.CreateScope();
            var erp = scope.ServiceProvider.GetRequiredService<ErpGateway>();
            var receipts = await erp.CallAsync(c => c.GetGoodsReceiptsAsync(site, true));
            foreach (var r in receipts ?? Array.Empty<ErpGoodsReceipt>())
            {
                await _bus.Publish(new GoodsReceived
                {
                    ReceiptNo = r.ReceiptNo,
                    LocationCode = r.SiteCode,
                    PurchaseOrderNo = r.PurchaseOrderNo,
                    ReceivedUtc = r.PostedAt.ToUniversalTime(),
                    Lines = (r.Lines ?? Array.Empty<ErpTransferLine>())
                        .Select(l => new StockLine { Sku = ToSku(erp, l), Quantity = (int)l.Quantity })
                        .ToList(),
                }, cancellationToken);
                await erp.CallAsync(c => c.AcknowledgeGoodsReceiptAsync(r.ReceiptNo));
                _logger.LogInformation("Goods receipt {ReceiptNo} at {Site} published", r.ReceiptNo, r.SiteCode);
            }
        }

        private static string ToSku(ErpGateway erp, ErpTransferLine line)
        {
            var style = line.ItemNo.StartsWith(erp.Settings.ItemPrefix, StringComparison.Ordinal)
                ? line.ItemNo.Substring(erp.Settings.ItemPrefix.Length)
                : line.ItemNo;
            return string.IsNullOrEmpty(line.VariantCode) ? style : style + "-" + line.VariantCode;
        }
    }

    public class ErpConnectorOptions
    {
        public int ReceiptPollSeconds { get; set; } = 300;
        /// <summary>"*" asks the ERP for receipts at every site (DC and store transfers).</summary>
        public string[] ReceiptSites { get; set; } = { "*" };
    }
}
