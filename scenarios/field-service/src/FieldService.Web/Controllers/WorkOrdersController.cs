using FieldService.Data;
using FieldService.Data.Entities;
using FieldService.Data.Services;
using FieldService.Web.Models;
using FieldService.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FieldService.Web.Controllers;

[Authorize]
public class WorkOrdersController : Controller
{
    private const string DispatchRoles = Roles.Dispatcher + "," + Roles.Admin;

    private readonly FieldServiceContext _db;
    private readonly WorkOrderService _orders;
    private readonly PhotoStore _photos;
    private readonly PortalOptions _options;
    private readonly ILogger<WorkOrdersController> _log;

    public WorkOrdersController(FieldServiceContext db, WorkOrderService orders, PhotoStore photos, IOptions<PortalOptions> options, ILogger<WorkOrdersController> log)
    {
        _db = db;
        _orders = orders;
        _photos = photos;
        _options = options.Value;
        _log = log;
    }

    public async Task<IActionResult> Index(BoardFilter? filter, bool apply = false)
    {
        if (apply && filter != null)
        {
            filter.Save(HttpContext.Session);
        }
        else
        {
            filter = BoardFilter.Load(HttpContext.Session);
        }

        var query = _orders.Query();
        if (User.IsInRole(Roles.Technician))
        {
            var techId = User.TechnicianId();
            query = query.Where(w => w.TechnicianId == techId);
        }
        else if (filter.TechnicianId.HasValue)
        {
            query = query.Where(w => w.TechnicianId == filter.TechnicianId);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(w => w.Status == filter.Status);
        }
        else if (filter.OpenOnly)
        {
            query = query.Where(w => w.Status == WorkOrderStatus.New || w.Status == WorkOrderStatus.Assigned || w.Status == WorkOrderStatus.InProgress);
        }

        if (!string.IsNullOrWhiteSpace(filter.Customer))
        {
            var term = filter.Customer.Trim();
            query = query.Where(w => w.Customer!.Name.Contains(term) || w.Customer.AccountCode.Contains(term));
        }

        var list = await query.OrderBy(w => w.DueUtc).Take(500).ToListAsync();
        if (filter.OverdueOnly)
        {
            var now = DateTime.UtcNow;
            list = list.Where(w => w.IsOverdue(now)).ToList();
        }

        return View(new BoardViewModel
        {
            Filter = filter,
            Orders = list,
            Technicians = await TechnicianListAsync(filter.TechnicianId)
        });
    }

    public async Task<IActionResult> Details(string id)
    {
        var order = await _orders.GetAsync(id);
        if (order == null)
        {
            return NotFound();
        }

        if (!CanSee(order))
        {
            return Forbid();
        }

        return View(new WorkOrderDetailsViewModel
        {
            Order = order,
            Technicians = await TechnicianListAsync(order.TechnicianId)
        });
    }

    [Authorize(Roles = DispatchRoles)]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadCustomersAsync();
        return View(new CreateWorkOrderViewModel());
    }

    [Authorize(Roles = DispatchRoles)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateWorkOrderViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await LoadCustomersAsync();
            return View(model);
        }

        try
        {
            var order = await _orders.CreateAsync(new NewWorkOrder(
                model.CustomerAccount, model.SiteAddress, model.Summary, model.Description, model.Priority,
                "Portal", model.CustomerReference));
            _log.LogInformation("{User} created {Number}", User.Identity?.Name, order.Number);
            TempData["Message"] = $"Work order {order.Number} created.";
            return RedirectToAction(nameof(Details), new { id = order.Number });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            await LoadCustomersAsync();
            return View(model);
        }
    }

    [Authorize(Roles = DispatchRoles)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Assign(string id, int technicianId) =>
        RunAsync(id, () => _orders.AssignAsync(id, technicianId), "Technician assigned.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Start(string id) =>
        RunAsync(id, () => _orders.StartAsync(id), "Work started.");

    [Authorize(Roles = DispatchRoles)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Cancel(string id) =>
        RunAsync(id, () => _orders.CancelAsync(id), "Work order cancelled.");

    [HttpGet]
    public async Task<IActionResult> Complete(string id)
    {
        var order = await _orders.GetAsync(id);
        if (order == null)
        {
            return NotFound();
        }

        if (!CanSee(order))
        {
            return Forbid();
        }

        return View(new CompleteWorkOrderViewModel { Number = order.Number, Summary = order.Summary });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(CompleteWorkOrderViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var parts = model.Parts
            .Where(p => !string.IsNullOrWhiteSpace(p.PartNumber))
            .Select(p => new WorkOrderPart { PartNumber = p.PartNumber!, Description = p.Description ?? "", Quantity = p.Quantity, UnitPrice = p.UnitPrice });
        try
        {
            await _orders.CompleteAsync(model.Number, model.LaborHours, model.ResolutionNotes, parts);
            _log.LogInformation("{User} completed {Number}", User.Identity?.Name, model.Number);
            TempData["Message"] = $"{model.Number} completed.";
            return RedirectToAction(nameof(Details), new { id = model.Number });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadPhoto(string id, IFormFile? photo, CancellationToken ct)
    {
        var order = await _orders.GetAsync(id, ct);
        if (order == null)
        {
            return NotFound();
        }

        if (photo == null || photo.Length == 0)
        {
            TempData["Error"] = "Choose a file to upload.";
        }
        else if (photo.Length > _options.MaxPhotoBytes)
        {
            TempData["Error"] = "The file is too large.";
        }
        else if (!PhotoStore.IsAllowed(photo.FileName))
        {
            TempData["Error"] = "Only JPG, PNG, HEIC and PDF files are accepted.";
        }
        else
        {
            var stored = await _photos.SaveAsync(order.Number, photo, ct);
            _db.WorkOrderPhotos.Add(new WorkOrderPhoto
            {
                WorkOrderId = order.Id,
                OriginalFileName = Path.GetFileName(photo.FileName),
                StoredFileName = stored,
                ContentType = PhotoStore.ContentTypeFor(photo.FileName),
                UploadedUtc = DateTime.UtcNow,
                UploadedBy = User.Identity?.Name ?? ""
            });
            await _db.SaveChangesAsync(ct);
            TempData["Message"] = "Photo uploaded.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Photo(string id, int photoId)
    {
        var order = await _orders.GetAsync(id);
        var photo = order?.Photos.SingleOrDefault(p => p.Id == photoId);
        if (order == null || photo == null)
        {
            return NotFound();
        }

        var stream = _photos.Open(order.Number, photo.StoredFileName);
        return stream == null ? NotFound() : File(stream, photo.ContentType, photo.OriginalFileName);
    }

    private bool CanSee(WorkOrder order) =>
        !User.IsInRole(Roles.Technician) || order.TechnicianId == User.TechnicianId();

    private async Task<IActionResult> RunAsync(string id, Func<Task> action, string message)
    {
        try
        {
            await action();
            TempData["Message"] = message;
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<List<SelectListItem>> TechnicianListAsync(int? selected) =>
        await _db.Technicians
            .Where(t => t.Active)
            .OrderBy(t => t.Name)
            .Select(t => new SelectListItem($"{t.Name} ({t.Region})", t.Id.ToString(), t.Id == selected))
            .ToListAsync();

    private async Task LoadCustomersAsync()
    {
        ViewBag.Customers = await _db.Customers
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem(c.Name, c.AccountCode))
            .ToListAsync();
    }
}
