using FieldService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FieldService.Data.Services;

public class WorkOrderService
{
    private readonly FieldServiceContext _db;
    private readonly NumberSequence _numbers;
    private readonly TimeProvider _clock;

    public WorkOrderService(FieldServiceContext db, NumberSequence numbers, TimeProvider clock)
    {
        _db = db;
        _numbers = numbers;
        _clock = clock;
    }

    public IQueryable<WorkOrder> Query() =>
        _db.WorkOrders.Include(w => w.Customer).Include(w => w.Technician);

    public Task<WorkOrder?> GetAsync(string number, CancellationToken ct = default) =>
        _db.WorkOrders
            .Include(w => w.Customer)
            .Include(w => w.Technician)
            .Include(w => w.Parts)
            .Include(w => w.Photos)
            .SingleOrDefaultAsync(w => w.Number == number, ct);

    public async Task<WorkOrder> CreateAsync(NewWorkOrder request, CancellationToken ct = default)
    {
        var customer = await _db.Customers.SingleOrDefaultAsync(c => c.AccountCode == request.CustomerAccount, ct)
            ?? throw new InvalidOperationException($"Unknown customer account '{request.CustomerAccount}'.");

        var now = _clock.GetUtcNow().UtcDateTime;
        var order = new WorkOrder
        {
            Number = await _numbers.NextAsync("WorkOrder", "WO", 5, now, ct),
            CustomerId = customer.Id,
            SiteAddress = request.SiteAddress.Trim(),
            Summary = request.Summary.Trim(),
            Description = request.Description?.Trim(),
            Priority = request.Priority,
            Status = WorkOrderStatus.New,
            CreatedUtc = now,
            DueUtc = SlaCalculator.DueDate(now, customer.SlaHours, request.Priority),
            Source = request.Source,
            CustomerReference = request.CustomerReference
        };

        _db.WorkOrders.Add(order);
        await _db.SaveChangesAsync(ct);
        return order;
    }

    public async Task AssignAsync(string number, int technicianId, CancellationToken ct = default)
    {
        var order = await RequireAsync(number, ct);
        if (!order.IsOpen)
        {
            throw new InvalidOperationException($"{number} is {order.Status} and cannot be reassigned.");
        }

        var tech = await _db.Technicians.FindAsync(new object[] { technicianId }, ct)
            ?? throw new InvalidOperationException("Unknown technician.");
        if (!tech.Active)
        {
            throw new InvalidOperationException($"{tech.Name} is not active.");
        }

        order.TechnicianId = tech.Id;
        if (order.Status == WorkOrderStatus.New)
        {
            order.Status = WorkOrderStatus.Assigned;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task StartAsync(string number, CancellationToken ct = default)
    {
        var order = await RequireAsync(number, ct);
        if (order.Status != WorkOrderStatus.Assigned)
        {
            throw new InvalidOperationException($"{number} must be assigned before work starts.");
        }

        order.Status = WorkOrderStatus.InProgress;
        await _db.SaveChangesAsync(ct);
    }

    public async Task CompleteAsync(string number, decimal laborHours, string? notes, IEnumerable<WorkOrderPart> parts, CancellationToken ct = default)
    {
        var order = await RequireAsync(number, ct);
        if (order.Status is not (WorkOrderStatus.Assigned or WorkOrderStatus.InProgress))
        {
            throw new InvalidOperationException($"{number} is {order.Status} and cannot be completed.");
        }

        if (laborHours <= 0 || laborHours > 24)
        {
            throw new InvalidOperationException("Labor hours must be between 0 and 24.");
        }

        order.LaborHours = laborHours;
        order.ResolutionNotes = notes;
        order.CompletedUtc = _clock.GetUtcNow().UtcDateTime;
        order.Status = WorkOrderStatus.Completed;
        foreach (var part in parts.Where(p => p.Quantity > 0 && !string.IsNullOrWhiteSpace(p.PartNumber)))
        {
            part.PartNumber = part.PartNumber.Trim().ToUpperInvariant();
            order.Parts.Add(part);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(string number, CancellationToken ct = default)
    {
        var order = await RequireAsync(number, ct);
        if (!order.IsOpen)
        {
            throw new InvalidOperationException($"{number} is {order.Status} and cannot be cancelled.");
        }

        order.Status = WorkOrderStatus.Cancelled;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<WorkOrder>> GetUninvoicedAsync(CancellationToken ct = default) =>
        await _db.WorkOrders
            .Include(w => w.Customer)
            .Include(w => w.Parts)
            .Where(w => w.Status == WorkOrderStatus.Completed && w.InvoiceBatch == null)
            .OrderBy(w => w.Number)
            .ToListAsync(ct);

    public async Task<List<WorkOrder>> GetOverdueAsync(CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var open = await Query()
            .Where(w => w.Status == WorkOrderStatus.New || w.Status == WorkOrderStatus.Assigned || w.Status == WorkOrderStatus.InProgress)
            .ToListAsync(ct);
        return open.Where(w => w.DueUtc < now).OrderBy(w => w.DueUtc).ToList();
    }

    private async Task<WorkOrder> RequireAsync(string number, CancellationToken ct) =>
        await _db.WorkOrders.Include(w => w.Parts).SingleOrDefaultAsync(w => w.Number == number, ct)
        ?? throw new InvalidOperationException($"Work order {number} was not found.");
}

public record NewWorkOrder(
    string CustomerAccount,
    string SiteAddress,
    string Summary,
    string? Description,
    WorkOrderPriority Priority,
    string Source = "Portal",
    string? CustomerReference = null);
