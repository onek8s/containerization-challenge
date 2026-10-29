using System;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanArsdel.Contracts.Catalog;
using VanArsdel.Contracts.Inventory;
using VanArsdel.Contracts.Pos;
using VanArsdel.Contracts.Returns;
using VanArsdel.Contracts.Stores;
using VanArsdel.Inventory.Api.Data;
using VanArsdel.Inventory.Api.Domain;
using VanArsdel.Inventory.Api.Services;

namespace VanArsdel.Inventory.Api.Consumers
{
    public class StoreConsumer : IConsumer<StoreOpened>, IConsumer<StoreUpdated>, IConsumer<StoreClosed>
    {
        private readonly InventoryDbContext _db;

        public StoreConsumer(InventoryDbContext db)
        {
            _db = db;
        }

        public async Task Consume(ConsumeContext<StoreOpened> context)
        {
            var m = context.Message;
            var location = await _db.Locations.FindAsync(m.StoreCode);
            if (location == null)
            {
                location = new Location { Code = m.StoreCode };
                _db.Locations.Add(location);
            }
            location.Name = m.Name;
            location.Type = m.LocationType ?? LocationTypes.Store;
            location.Country = m.Country;
            location.City = m.City;
            location.ShipFromStore = m.ShipFromStore;
            location.ClickAndCollect = m.ClickAndCollect;
            location.Active = true;
            location.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task Consume(ConsumeContext<StoreUpdated> context)
        {
            var location = await _db.Locations.FindAsync(context.Message.StoreCode);
            if (location == null) return;
            location.Name = context.Message.Name;
            location.ShipFromStore = context.Message.ShipFromStore;
            location.ClickAndCollect = context.Message.ClickAndCollect;
            location.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task Consume(ConsumeContext<StoreClosed> context)
        {
            var location = await _db.Locations.FindAsync(context.Message.StoreCode);
            if (location == null) return;
            location.Active = false;
            location.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public class ItemConsumer : IConsumer<ProductPublished>, IConsumer<ProductDiscontinued>
    {
        private readonly InventoryDbContext _db;

        public ItemConsumer(InventoryDbContext db)
        {
            _db = db;
        }

        public async Task Consume(ConsumeContext<ProductPublished> context)
        {
            var m = context.Message;
            var skus = m.Skus.Select(s => s.Sku).ToList();
            var existing = await _db.Items.Where(i => skus.Contains(i.Sku)).Select(i => i.Sku).ToListAsync();
            foreach (var sku in m.Skus.Where(s => !existing.Contains(s.Sku)))
            {
                _db.Items.Add(new Item { Sku = sku.Sku, StyleCode = m.StyleCode, Ean = sku.Ean, CategoryCode = m.CategoryCode, CreatedUtc = DateTime.UtcNow });
            }
            await _db.SaveChangesAsync();
        }

        public async Task Consume(ConsumeContext<ProductDiscontinued> context)
        {
            var items = await _db.Items.Where(i => i.StyleCode == context.Message.StyleCode).ToListAsync();
            items.ForEach(i => i.Discontinued = true);
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>Store sales from the POS feed reduce store stock; POS returns add to it.</summary>
    public class StoreSalesConsumer : IConsumer<StoreSalesReceived>
    {
        private readonly StockService _stock;
        private readonly ILogger<StoreSalesConsumer> _logger;

        public StoreSalesConsumer(StockService stock, ILogger<StoreSalesConsumer> logger)
        {
            _stock = stock;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<StoreSalesReceived> context)
        {
            var m = context.Message;
            var deltas = m.Receipts.SelectMany(r => r.Lines.Select(l => new StockDelta(l.Sku, r.IsReturn ? Math.Abs(l.Quantity) : -Math.Abs(l.Quantity)))).ToList();
            var applied = await _stock.ApplyAsync(m.StoreCode, deltas, MovementTypes.Sale, m.FileName, "pos-feed", "pos:" + m.BatchId, context.CancellationToken);
            _logger.LogInformation("POS file {File} from {Store}: {Receipts} receipts, applied {Applied}", m.FileName, m.StoreCode, m.Receipts.Count, applied);
        }
    }

    public class GoodsReceivedConsumer : IConsumer<GoodsReceived>, IConsumer<TransferShipped>
    {
        private readonly StockService _stock;
        private readonly InventoryDbContext _db;

        public GoodsReceivedConsumer(StockService stock, InventoryDbContext db)
        {
            _stock = stock;
            _db = db;
        }

        public Task Consume(ConsumeContext<GoodsReceived> context)
        {
            var m = context.Message;
            return _stock.ApplyAsync(m.LocationCode, m.Lines.Select(l => new StockDelta(l.Sku, l.Quantity)), MovementTypes.Receipt,
                m.ReceiptNo, "erp", "receipt:" + m.ReceiptNo, context.CancellationToken);
        }

        /// <summary>The ERP shipped a transfer: DC stock leaves, the store has it in transit until its POS receipt.</summary>
        public async Task Consume(ConsumeContext<TransferShipped> context)
        {
            var m = context.Message;
            var applied = await _stock.ApplyAsync(m.FromLocation, m.Lines.Select(l => new StockDelta(l.Sku, -l.Quantity)), MovementTypes.TransferOut,
                m.TransferNo, "erp", "transfer-out:" + m.TransferNo, context.CancellationToken);
            if (!applied) return;
            var skus = m.Lines.Select(l => l.Sku).ToList();
            var dcLevels = await _db.StockLevels.Where(s => s.LocationCode == m.FromLocation && skus.Contains(s.Sku)).ToListAsync();
            foreach (var l in m.Lines)
            {
                var dc = dcLevels.SingleOrDefault(s => s.Sku == l.Sku);
                if (dc != null) dc.Reserved = Math.Max(0, dc.Reserved - l.Quantity);
            }
            await _db.SaveChangesAsync();
        }
    }

    public class ReturnRestockedConsumer : IConsumer<ReturnRestocked>
    {
        private readonly StockService _stock;

        public ReturnRestockedConsumer(StockService stock)
        {
            _stock = stock;
        }

        public Task Consume(ConsumeContext<ReturnRestocked> context)
        {
            var m = context.Message;
            var sellable = m.Lines.Where(l => l.Grade == "A" || l.Grade == "B").Select(l => new StockDelta(l.Sku, l.Quantity)).ToList();
            if (sellable.Count == 0) return Task.CompletedTask;
            return _stock.ApplyAsync(m.LocationCode, sellable, MovementTypes.ReturnRestock, m.RmaNo, "returns", "return:" + m.ReturnId, context.CancellationToken);
        }
    }
}
