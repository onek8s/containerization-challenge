using ContosoFoods.TradeRebates.Core;
using ContosoFoods.TradeRebates.Data;
using ContosoFoods.TradeRebates.Rules;
using Microsoft.AspNetCore.Mvc;

namespace ContosoFoods.TradeRebates.Portal.Pages.Distributors;

public class IndexModel(IPortalStore store) : PortalPageModel
{
    public IReadOnlyList<Distributor> Distributors { get; private set; } = [];
    public Dictionary<int, string> Revealed { get; } = [];

    public async Task OnGetAsync() => Distributors = await store.ListDistributorsAsync();

    public async Task<IActionResult> OnPostRevealAsync(int id)
    {
        string? iban = await store.RevealBankAccountAsync(id, Actor);
        if (iban is not null)
            Revealed[id] = iban;
        Distributors = await store.ListDistributorsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostBankAsync(int id, string? iban)
    {
        if (!Iban.IsValid(iban))
        {
            TempData["Error"] = "The IBAN is not valid.";
            return RedirectToPage();
        }
        return await TryAsync(async () =>
        {
            await store.SaveBankAccountAsync(id, Iban.Normalize(iban), Actor);
            TempData["Message"] = "Bank account saved.";
            return RedirectToPage();
        }, () => RedirectToPage());
    }
}
