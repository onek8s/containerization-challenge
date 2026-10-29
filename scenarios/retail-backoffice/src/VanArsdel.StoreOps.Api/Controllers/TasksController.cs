using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanArsdel.BuildingBlocks.Exceptions;
using VanArsdel.BuildingBlocks.Outbox;
using VanArsdel.BuildingBlocks.Security;
using VanArsdel.Contracts.Orders;
using VanArsdel.StoreOps.Api.Data;
using VanArsdel.StoreOps.Api.Domain;
using VanArsdel.StoreOps.Api.Services;

namespace VanArsdel.StoreOps.Api.Controllers
{
    public class PickedLine
    {
        public string Sku { get; set; }
        public int Picked { get; set; }
    }

    public class PackTask
    {
        public int Parcels { get; set; } = 1;
        public decimal WeightKg { get; set; }
        public List<PickedLine> Lines { get; set; } = new List<PickedLine>();
    }

    [ApiController]
    [Route("api/tasks")]
    [Authorize(Policy = Policies.Read)]
    public class TasksController : ControllerBase
    {
        private readonly StoreOpsDbContext _db;
        private readonly IOutbox _outbox;
        private readonly StoreNotifier _notifier;

        public TasksController(StoreOpsDbContext db, IOutbox outbox, StoreNotifier notifier)
        {
            _db = db;
            _outbox = outbox;
            _notifier = notifier;
        }

        [HttpGet]
        public async Task<List<PickTask>> List([FromQuery] string store, [FromQuery] string status, [FromQuery] int take = 100)
        {
            var storeCode = StoreAccess.Resolve(User, store);
            var q = _db.PickTasks.AsNoTracking().Include(t => t.Lines).AsQueryable();
            if (storeCode != null) q = q.Where(t => t.StoreCode == storeCode);
            q = string.IsNullOrEmpty(status)
                ? q.Where(t => t.Status == PickTaskStatus.Open || t.Status == PickTaskStatus.Picking)
                : q.Where(t => t.Status == status);
            return await q.OrderBy(t => t.DueUtc).Take(Math.Min(take, 500)).ToListAsync();
        }

        [HttpGet("{id:guid}")]
        public async Task<PickTask> Get(Guid id)
        {
            var task = await _db.PickTasks.AsNoTracking().Include(t => t.Lines).SingleOrDefaultAsync(t => t.Id == id)
                       ?? throw new NotFoundException("Pick task", id);
            StoreAccess.Resolve(User, task.StoreCode);
            return task;
        }

        [HttpPost("{id:guid}/start")]
        [Authorize(Policy = Policies.Store)]
        public async Task<PickTask> Start(Guid id)
        {
            var task = await Load(id);
            if (task.Status != PickTaskStatus.Open && task.Status != PickTaskStatus.Picking)
            {
                throw new ConflictException("Task is " + task.Status);
            }
            task.Status = PickTaskStatus.Picking;
            task.AssignedTo = User.UserName();
            await _db.SaveChangesAsync();
            await _notifier.SendAsync(task.StoreCode, StoreNotifier.TaskChanged, new { task.Id, task.Status, task.AssignedTo });
            return task;
        }

        /// <summary>Marks the task packed. Orders books the carrier, or puts the parcel on the collection shelf.</summary>
        [HttpPost("{id:guid}/packed")]
        [Authorize(Policy = Policies.Store)]
        public async Task<PickTask> Packed(Guid id, [FromBody] PackTask command)
        {
            var task = await Load(id);
            if (task.Status == PickTaskStatus.Packed) return task;
            if (task.Status == PickTaskStatus.Cancelled) throw new ConflictException("The order was cancelled.");

            foreach (var line in task.Lines)
            {
                var picked = command.Lines.FirstOrDefault(l => string.Equals(l.Sku, line.Sku, StringComparison.OrdinalIgnoreCase));
                line.Picked = picked?.Picked ?? line.Quantity;
            }
            var shorts = task.Lines.Where(l => l.Picked < l.Quantity).Select(l => l.Sku).ToList();
            if (shorts.Count > 0)
            {
                // Short picks go back to customer service; partial shipments are not supported by the carrier contract.
                throw new DomainException("Short picked: " + string.Join(", ", shorts) + ". Report the shortage to customer service.");
            }

            task.Status = PickTaskStatus.Packed;
            task.Parcels = Math.Max(1, command.Parcels);
            task.WeightKg = command.WeightKg > 0 ? command.WeightKg : 0.5m * task.Lines.Sum(l => l.Quantity);
            task.PackedUtc = DateTime.UtcNow;
            task.AssignedTo ??= User.UserName();
            _outbox.Add(new ShipmentPacked
            {
                OrderId = task.OrderId, ShipmentId = task.ShipmentId, Parcels = task.Parcels, WeightKg = task.WeightKg, PackedBy = task.AssignedTo,
            });
            await _db.SaveChangesAsync();
            await _notifier.SendAsync(task.StoreCode, StoreNotifier.TaskChanged, new { task.Id, task.Status });
            return task;
        }

        private async Task<PickTask> Load(Guid id)
        {
            var task = await _db.PickTasks.Include(t => t.Lines).SingleOrDefaultAsync(t => t.Id == id)
                       ?? throw new NotFoundException("Pick task", id);
            StoreAccess.Resolve(User, task.StoreCode);
            return task;
        }
    }

    [ApiController]
    [Route("api/collections")]
    [Authorize(Policy = Policies.Read)]
    public class CollectionsController : ControllerBase
    {
        private readonly StoreOpsDbContext _db;
        private readonly IOutbox _outbox;
        private readonly StoreNotifier _notifier;

        public CollectionsController(StoreOpsDbContext db, IOutbox outbox, StoreNotifier notifier)
        {
            _db = db;
            _outbox = outbox;
            _notifier = notifier;
        }

        [HttpGet]
        public async Task<List<Collection>> List([FromQuery] string store, [FromQuery] string status = CollectionStatus.Waiting)
        {
            var storeCode = StoreAccess.Resolve(User, store);
            var q = _db.Collections.AsNoTracking().Where(c => c.Status == status);
            if (storeCode != null) q = q.Where(c => c.StoreCode == storeCode);
            return await q.OrderBy(c => c.CollectBy).Take(500).ToListAsync();
        }

        /// <summary>Finds the parcel by order number (customer shows the pickup e-mail at the till).</summary>
        [HttpGet("find/{orderNo}")]
        public async Task<Collection> Find(string orderNo, [FromQuery] string store)
        {
            var storeCode = StoreAccess.Resolve(User, store);
            var q = _db.Collections.AsNoTracking().Where(c => c.OrderNo == orderNo.ToUpperInvariant());
            if (storeCode != null) q = q.Where(c => c.StoreCode == storeCode);
            return await q.OrderByDescending(c => c.ReadyUtc).FirstOrDefaultAsync() ?? throw new NotFoundException("Collection", orderNo);
        }

        [HttpPost("{id:long}/handover")]
        [Authorize(Policy = Policies.Store)]
        public async Task<Collection> Handover(long id)
        {
            var collection = await _db.Collections.FindAsync(id) ?? throw new NotFoundException("Collection", id);
            StoreAccess.Resolve(User, collection.StoreCode);
            if (collection.Status == CollectionStatus.Collected) return collection;
            if (collection.Status != CollectionStatus.Waiting) throw new ConflictException("Collection is " + collection.Status);

            collection.Status = CollectionStatus.Collected;
            collection.CollectedUtc = DateTime.UtcNow;
            collection.HandedOverBy = User.UserName();
            _outbox.Add(new OrderCollected { OrderId = collection.OrderId, StoreCode = collection.StoreCode, HandedOverBy = collection.HandedOverBy });
            await _db.SaveChangesAsync();
            await _notifier.SendAsync(collection.StoreCode, StoreNotifier.CollectionChanged, new { collection.Id, collection.Status });
            return collection;
        }

        /// <summary>Parcels past the collect-by date go back to stock. Called by the scheduler.</summary>
        [HttpPost("expire")]
        [Authorize(Policy = Policies.Manage)]
        public async Task<object> Expire()
        {
            var now = DateTime.UtcNow;
            var expired = await _db.Collections.Where(c => c.Status == CollectionStatus.Waiting && c.CollectBy < now).ToListAsync();
            foreach (var c in expired)
            {
                c.Status = CollectionStatus.Expired;
                _outbox.Add(new CollectionExpired { OrderId = c.OrderId, StoreCode = c.StoreCode, CollectBy = c.CollectBy });
            }
            await _db.SaveChangesAsync();
            return new { Expired = expired.Count };
        }
    }
}
