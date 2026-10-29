using System.ComponentModel.DataAnnotations;
using FreightRates.Data;
using FreightRates.Data.Entities;
using FreightRates.Data.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FreightRates.Admin.Pages.Customers;

public class EditModel : PageModel
{
    private readonly FreightRatesContext _db;
    private readonly CustomerService _customers;

    public EditModel(FreightRatesContext db, CustomerService customers)
    {
        _db = db;
        _customers = customers;
    }

    [BindProperty]
    public CustomerInput Input { get; set; } = new();

    public string? NewApiKey { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken ct)
    {
        if (id is null)
            return Page();

        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null)
            return NotFound();
        Input = CustomerInput.From(customer);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Page();

        Customer? customer;
        if (Input.Id == 0)
        {
            var code = Input.Code.Trim().ToUpperInvariant();
            if (await _db.Customers.AnyAsync(c => c.Code == code, ct))
            {
                ModelState.AddModelError("Input.Code", $"Customer {code} already exists.");
                return Page();
            }
            customer = new Customer { Code = code, ApiKeyHash = "" };
            _db.Customers.Add(customer);
        }
        else
        {
            customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == Input.Id, ct);
            if (customer is null)
                return NotFound();
        }

        customer.Name = Input.Name.Trim();
        customer.DiscountPercent = Input.DiscountPercent;
        customer.MinimumCharge = Input.MinimumCharge;
        customer.FuelSurchargeOverridePercent = Input.FuelSurchargeOverridePercent;
        customer.Active = Input.Active;
        customer.ModifiedUtc = DateTime.UtcNow;
        customer.ModifiedBy = User.Identity?.Name;
        await _db.SaveChangesAsync(ct);

        TempData["Message"] = $"Customer {customer.Code} saved.";
        return RedirectToPage("Edit", new { id = customer.Id });
    }

    public async Task<IActionResult> OnPostRotateKeyAsync(int id, CancellationToken ct)
    {
        NewApiKey = await _customers.RotateApiKeyAsync(id, User.Identity?.Name ?? "", ct);
        var customer = await _db.Customers.AsNoTracking().FirstAsync(c => c.Id == id, ct);
        Input = CustomerInput.From(customer);
        ModelState.Clear();
        return Page();
    }

    public class CustomerInput
    {
        public int Id { get; set; }

        [Required, StringLength(12, MinimumLength = 2), RegularExpression("^[A-Za-z0-9]+$")]
        public string Code { get; set; } = "";

        [Required, StringLength(100)]
        public string Name { get; set; } = "";

        [Range(0, 95)]
        public decimal DiscountPercent { get; set; }

        [Range(0, 10000)]
        public decimal MinimumCharge { get; set; }

        [Range(0, 100)]
        public decimal? FuelSurchargeOverridePercent { get; set; }

        public bool Active { get; set; } = true;

        public DateTime? ModifiedUtc { get; set; }
        public string? ModifiedBy { get; set; }

        public static CustomerInput From(Customer c) => new()
        {
            Id = c.Id,
            Code = c.Code,
            Name = c.Name,
            DiscountPercent = c.DiscountPercent,
            MinimumCharge = c.MinimumCharge,
            FuelSurchargeOverridePercent = c.FuelSurchargeOverridePercent,
            Active = c.Active,
            ModifiedUtc = c.ModifiedUtc,
            ModifiedBy = c.ModifiedBy,
        };
    }
}
