using ContosoFoods.TradeRebates.Core;
using ContosoFoods.TradeRebates.Data;
using ContosoFoods.TradeRebates.Rules;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ContosoFoods.TradeRebates.Portal.Pages.Agreements;

public class EditModel(IPortalStore store) : PortalPageModel
{
    [BindProperty] public AgreementInput Input { get; set; } = new();
    [BindProperty] public string? RowVer { get; set; }

    public IEnumerable<SelectListItem> DistributorItems { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        await LoadListsAsync();
        if (id is null)
        {
            int year = DateTime.Today.Year;
            Input = new AgreementInput
            {
                AgreementNo = $"TR-{year}-",
                Basis = "V",
                Tiers = "R;0:0",
                ValidFrom = new DateTime(year, 1, 1),
                ValidTo = new DateTime(year, 12, 31)
            };
            return Page();
        }

        var (a, _) = await store.GetAgreementAsync(id.Value);
        if (a is null)
            return NotFound();
        if (a.Status != "D")
        {
            TempData["Error"] = "Only draft agreements can be changed.";
            return RedirectToPage("Details", new { id });
        }

        Input = new AgreementInput
        {
            AgreementId = a.AgreementId,
            AgreementNo = a.AgreementNo,
            DistributorId = a.DistributorId,
            Description = a.Description,
            Basis = a.Basis,
            Category = a.Category,
            Tiers = a.TiersText,
            ValidFrom = a.ValidFrom,
            ValidTo = a.ValidTo,
            CapAmount = a.CapAmount
        };
        RowVer = Convert.ToBase64String(a.RowVer);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Input.AgreementNo))
            ModelState.AddModelError("Input.AgreementNo", "Agreement number is required.");
        if (string.IsNullOrWhiteSpace(Input.Description))
            ModelState.AddModelError("Input.Description", "Description is required.");
        if (!TierSchedule.TryParse(Input.Tiers ?? "", out _, out string? tierError))
            ModelState.AddModelError("Input.Tiers", tierError ?? "Tiers are not valid.");
        if (Input.ValidTo < Input.ValidFrom)
            ModelState.AddModelError("Input.ValidTo", "Valid to must be on or after valid from.");

        if (!ModelState.IsValid)
        {
            await LoadListsAsync();
            return Page();
        }

        Input.RowVer = string.IsNullOrEmpty(RowVer) ? null : Convert.FromBase64String(RowVer);
        try
        {
            int id = await store.SaveAgreementAsync(Input, Actor);
            TempData["Message"] = "Agreement saved.";
            return RedirectToPage("Details", new { id });
        }
        catch (RebateRuleException ex)
        {
            ModelState.AddModelError("", ex.Message);
            await LoadListsAsync();
            return Page();
        }
    }

    private async Task LoadListsAsync() =>
        DistributorItems = (await store.ListDistributorsAsync()).Select(d => new SelectListItem(d.Name, d.DistributorId.ToString()));
}
