using System.Diagnostics;
using FieldService.Data;
using FieldService.Data.Entities;
using FieldService.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FieldService.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly FieldServiceContext _db;

    public HomeController(FieldServiceContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var now = DateTime.UtcNow;
        var counts = await _db.WorkOrders
            .GroupBy(w => w.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var openDue = await _db.WorkOrders
            .Where(w => w.Status == WorkOrderStatus.New || w.Status == WorkOrderStatus.Assigned || w.Status == WorkOrderStatus.InProgress)
            .Select(w => w.DueUtc)
            .ToListAsync();

        var model = new DashboardViewModel
        {
            StatusCounts = counts.ToDictionary(c => c.Status, c => c.Count),
            OverdueCount = openDue.Count(d => d < now),
            RecentImports = await _db.ImportedFiles.OrderByDescending(f => f.Id).Take(5).ToListAsync(),
            RecentBatches = await _db.InvoiceBatches.OrderByDescending(b => b.Id).Take(5).ToListAsync()
        };
        return View(model);
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
