using ContosoFoods.TradeRebates.Core;
using ContosoFoods.TradeRebates.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ContosoFoods.TradeRebates.Portal.Pages.Claims;

public class DetailsModel(IPortalStore store, IAuthorizationService auth) : PortalPageModel
{
    public ClaimDetail? Claim { get; private set; }
    public bool CanDecide { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Claim = await store.GetClaimAsync(id);
        if (Claim is null)
            return NotFound();
        CanDecide = (await auth.AuthorizeAsync(User, Policies.Approver)).Succeeded;
        return Page();
    }

    public Task<IActionResult> OnPostValidateAsync(int id) => TryAsync(async () =>
    {
        await store.ValidateClaimAsync(id);
        TempData["Message"] = "Claim validated again.";
        return RedirectToPage(new { id });
    }, () => RedirectToPage(new { id }));

    public async Task<IActionResult> OnPostDecideAsync(int id, string rowVer, bool approve, string? note)
    {
        if (!(await auth.AuthorizeAsync(User, Policies.Approver)).Succeeded)
            return Forbid();

        return await TryAsync(async () =>
        {
            await store.DecideClaimAsync(id, Convert.FromBase64String(rowVer), approve, Actor, string.IsNullOrWhiteSpace(note) ? null : note.Trim());
            TempData["Message"] = approve ? "Claim approved." : "Claim rejected.";
            return RedirectToPage(new { id });
        }, () => RedirectToPage(new { id }));
    }
}
