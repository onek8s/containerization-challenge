using System;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VanArsdel.Contracts.Orders;
using VanArsdel.StoreOps.Api.Data;
using VanArsdel.StoreOps.Api.Domain;
using VanArsdel.StoreOps.Api.Services;

namespace VanArsdel.StoreOps.Api.Consumers
{
    /// <summary>Pick tasks and the collection shelf for ship-from-store and click and collect orders.</summary>
    public class FulfilmentConsumers :
        IConsumer<ShipmentRequested>,
        IConsumer<OrderReadyForCollection>,
        IConsumer<OrderCancelled>
    {
        private readonly StoreOpsDbContext _db;
        private readonly StoreNotifier _notifier;
        private readonly IConfiguration _configuration;
        private readonly ILogger<FulfilmentConsumers> _logger;

        public FulfilmentConsumers(StoreOpsDbContext db, StoreNotifier notifier, IConfiguration configuration, ILogger<FulfilmentConsumers> logger)
        {
            _db = db;
            _notifier = notifier;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<ShipmentRequested> context)
        {
            var m = context.Message;
            if (await _db.PickTasks.AnyAsync(t => t.ShipmentId == m.ShipmentId))
            {
                return;
            }

            // DC shipments get the same task list; the DC team uses the store manager app on handhelds.
            var hours = m.DeliveryMethod == DeliveryMethods.ClickAndCollect
                ? _configuration.GetValue("PickTasks:CollectDueHours", 4)
                : _configuration.GetValue("PickTasks:ShipDueHours", 24);

            var task = new PickTask
            {
                Id = Guid.NewGuid(),
                StoreCode = m.FromLocation,
                OrderId = m.OrderId,
                OrderNo = m.OrderNo,
                ShipmentId = m.ShipmentId,
                DeliveryMethod = m.DeliveryMethod,
                CustomerName = m.CustomerName,
                CreatedUtc = DateTime.UtcNow,
                DueUtc = DateTime.UtcNow.AddHours(hours),
                Lines = m.Lines.Select(l => new PickTaskLine { Sku = l.Sku, Quantity = l.Quantity }).ToList(),
            };
            _db.PickTasks.Add(task);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Pick task {TaskId} for order {OrderNo} at {Store}", task.Id, task.OrderNo, task.StoreCode);

            await _notifier.SendAsync(task.StoreCode, StoreNotifier.TaskCreated, new
            {
                task.Id, task.OrderNo, task.DeliveryMethod, task.CustomerName, task.DueUtc,
                Items = task.Lines.Sum(l => l.Quantity),
            });
        }

        public async Task Consume(ConsumeContext<OrderReadyForCollection> context)
        {
            var m = context.Message;
            if (await _db.Collections.AnyAsync(c => c.OrderId == m.OrderId && c.StoreCode == m.StoreCode))
            {
                return;
            }
            var collection = new Collection
            {
                StoreCode = m.StoreCode,
                OrderId = m.OrderId,
                OrderNo = m.OrderNo,
                CustomerEmail = m.CustomerEmail,
                CustomerPhone = m.CustomerPhone,
                ReadyUtc = DateTime.UtcNow,
                CollectBy = m.CollectBy,
            };
            _db.Collections.Add(collection);
            await _db.SaveChangesAsync();
            await _notifier.SendAsync(m.StoreCode, StoreNotifier.CollectionReady, new { collection.Id, collection.OrderNo, collection.CollectBy });
        }

        public async Task Consume(ConsumeContext<OrderCancelled> context)
        {
            var orderId = context.Message.OrderId;
            var tasks = await _db.PickTasks
                .Where(t => t.OrderId == orderId && (t.Status == PickTaskStatus.Open || t.Status == PickTaskStatus.Picking))
                .ToListAsync();
            foreach (var task in tasks)
            {
                task.Status = PickTaskStatus.Cancelled;
            }
            var collections = await _db.Collections.Where(c => c.OrderId == orderId && c.Status == CollectionStatus.Waiting).ToListAsync();
            foreach (var c in collections)
            {
                c.Status = CollectionStatus.Expired;
            }
            await _db.SaveChangesAsync();

            foreach (var task in tasks)
            {
                await _notifier.SendAsync(task.StoreCode, StoreNotifier.TaskChanged, new { task.Id, task.Status });
            }
        }
    }
}
