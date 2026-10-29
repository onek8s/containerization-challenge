using AutoMapper;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Booking.Models;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain.Parties;

namespace Proseware.ControlTower.Booking.Controllers;

[Authorize(Policy = Policies.Admin)]
public class CustomersController : Controller
{
    private readonly ControlTowerDbContext _db;
    private readonly IMapper _mapper;
    private readonly IValidator<CustomerEditModel> _validator;

    public CustomersController(ControlTowerDbContext db, IMapper mapper, IValidator<CustomerEditModel> validator)
    {
        _db = db;
        _mapper = mapper;
        _validator = validator;
    }

    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await _db.Customers.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct));

    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        if (id is null) return View(new CustomerEditModel());
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        return customer is null ? NotFound() : View(_mapper.Map<CustomerEditModel>(customer));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CustomerEditModel model, CancellationToken ct)
    {
        model.Code = model.Code?.Trim().ToUpperInvariant() ?? "";
        var result = await _validator.ValidateAsync(model, ct);
        foreach (var e in result.Errors) ModelState.AddModelError(e.PropertyName, e.ErrorMessage);
        if (await _db.Customers.AnyAsync(c => c.Code == model.Code && c.Id != model.Id, ct))
            ModelState.AddModelError(nameof(model.Code), "The code is in use.");
        if (!ModelState.IsValid) return View(model);

        Customer customer;
        if (model.Id == 0)
        {
            customer = new Customer { CreatedUtc = DateTime.UtcNow };
            _db.Customers.Add(customer);
        }
        else
        {
            customer = await _db.Customers.FirstAsync(c => c.Id == model.Id, ct);
        }
        _mapper.Map(model, customer);
        await _db.SaveChangesAsync(ct);
        TempData["Message"] = $"Customer {customer.Code} saved.";
        return RedirectToAction(nameof(Index));
    }
}
