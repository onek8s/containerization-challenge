using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PolicyChanges.Data;
using PolicyChanges.Data.Entities;
using PolicyChanges.Data.Services;

namespace PolicyChanges.Portal.Pages.Partners;

public class IndexModel : PageModel
{
    private readonly PolicyChangesContext _db;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(PolicyChangesContext db, ILogger<IndexModel> logger)
    {
        _db = db;
        _logger = logger;
    }

    public List<Partner> Partners { get; private set; } = new();

    /// <summary>Shown once after a key is issued.</summary>
    public string? NewKey { get; private set; }

    public async Task OnGetAsync() => Partners = await _db.Partners.OrderBy(p => p.Code).ToListAsync();

    public async Task<IActionResult> OnPostAddAsync(string code, string name)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        if (!Regex.IsMatch(code, "^[A-Z0-9]{3,20}$") || string.IsNullOrWhiteSpace(name) || await _db.Partners.AnyAsync(p => p.Code == code))
        {
            TempData["Error"] = "Code must be 3-20 letters or digits and not in use; name is required.";
            return RedirectToPage();
        }
        var key = ApiKeys.Generate(code);
        _db.Partners.Add(new Partner { Code = code, Name = name.Trim(), ApiKeyHash = ApiKeys.Hash(key), CreatedUtc = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        _logger.LogInformation("Partner {Code} added by {User}", code, User.Identity?.Name);
        NewKey = key;
        await OnGetAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostIssueKeyAsync(int id)
    {
        var p = await _db.Partners.FindAsync(id);
        if (p is null) return NotFound();
        var key = ApiKeys.Generate(p.Code);
        p.ApiKeyHash = ApiKeys.Hash(key);
        p.Active = true;
        await _db.SaveChangesAsync();
        _logger.LogInformation("New API key for partner {Code} issued by {User}", p.Code, User.Identity?.Name);
        NewKey = key;
        await OnGetAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var p = await _db.Partners.FindAsync(id);
        if (p is null) return NotFound();
        p.Active = !p.Active;
        await _db.SaveChangesAsync();
        TempData["Message"] = $"{p.Code} {(p.Active ? "enabled" : "disabled")}.";
        return RedirectToPage();
    }
}
