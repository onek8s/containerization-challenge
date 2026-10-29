using FieldService.Data;
using FieldService.Data.Entities;
using FieldService.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FieldService.Web.Controllers;

[Authorize(Roles = Roles.Dispatcher + "," + Roles.Admin)]
public class CustomersController : Controller
{
    private readonly FieldServiceContext _db;

    public CustomersController(FieldServiceContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var rows = await _db.Customers
            .OrderBy(c => c.Name)
            .Select(c => new CustomerRow
            {
                Customer = c,
                OpenOrders = c.WorkOrders.Count(w => w.Status == WorkOrderStatus.New || w.Status == WorkOrderStatus.Assigned || w.Status == WorkOrderStatus.InProgress),
                TotalOrders = c.WorkOrders.Count
            })
            .ToListAsync();
        return View(rows);
    }
}
