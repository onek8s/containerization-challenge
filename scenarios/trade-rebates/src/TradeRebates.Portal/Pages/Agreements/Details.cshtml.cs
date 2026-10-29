using ContosoFoods.TradeRebates.Core;
using ContosoFoods.TradeRebates.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ContosoFoods.TradeRebates.Portal.Pages.Agreements;

public class DetailsModel(IPortalStore store, IAuthorizationService auth) : PortalPageModel
{
    public AgreementDetail? Agreement { get; private set; }
    public IReadOnlyList<AccrualRow> Accruals { get; private set; } = [];
    public bool CanApprove { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        (Agreement, Accruals) = await store.GetAgreementAsync(id);
        if (Agreement is null)
            return NotFound();
        CanApprove = (await auth.AuthorizeAsync(User, Policies.Approver)).Succeeded;
        return Page();
    }

    public async Task<IActionResult> OnPostStatusAsync(int id, string status)
    {
        if (!(await auth.AuthorizeAsync(User, Policies.Approver)).Succeeded)
            return Forbid();
        return await TryAsync(async () =>
        {
            await store.SetAgreementStatusAsync(id, status, Actor);
            TempData["Message"] = status == "A" ? "Agreement activated." : "Agreement closed.";
            return RedirectToPage(new { id });
        }, () => RedirectToPage(new { id }));
    }
}
