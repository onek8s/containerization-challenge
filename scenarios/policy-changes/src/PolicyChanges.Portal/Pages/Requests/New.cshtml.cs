using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PolicyChanges.Core.Requests;
using PolicyChanges.Data.Services;

namespace PolicyChanges.Portal.Pages.Requests;

public class NewModel : PageModel
{
    private readonly ChangeService _changes;
    private readonly ChangeValidator _validator;

    public NewModel(ChangeService changes, ChangeValidator validator)
    {
        _changes = changes;
        _validator = validator;
    }

    [BindProperty(SupportsGet = true)] public ChangeType Type { get; set; } = ChangeType.AddressChange;
    [BindProperty] public string PolicyNumber { get; set; } = "";
    [BindProperty] public DateOnly EffectiveDate { get; set; }
    [BindProperty] public Dictionary<string, string> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Errors { get; private set; } = Array.Empty<string>();

    public string Field(string name) => Fields.TryGetValue(name, out var v) ? v : "";

    public void OnGet()
    {
        EffectiveDate = _validator.Today;
        if (Type == ChangeType.PaymentMethodChange) Fields[ChangeFields.BillingDay] = "15";
        if (Type == ChangeType.AddressChange) Fields[ChangeFields.Country] = "FI";
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var draft = new ChangeDraft
        {
            PolicyNumber = PolicyNumber,
            ChangeType = Type,
            EffectiveDate = EffectiveDate,
            Fields = new Dictionary<string, string>(
                Fields.Where(f => ChangeFields.For(Type).Contains(f.Key, StringComparer.OrdinalIgnoreCase)
                                  && !string.IsNullOrWhiteSpace(f.Value)),
                StringComparer.OrdinalIgnoreCase),
        };
        var result = await _changes.SubmitAsync(draft, User.Identity?.Name ?? "unknown", ChangeChannel.Portal, ct: ct);
        if (!result.Succeeded)
        {
            Errors = result.Errors;
            return Page();
        }
        TempData["Message"] = $"{result.Request!.Reference} queued for PolicyDesk.";
        return RedirectToPage("Details", new { reference = result.Request.Reference });
    }
}
