using ContosoFoods.TradeRebates.Core;
using ContosoFoods.TradeRebates.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ContosoFoods.TradeRebates.Portal.Pages.Statements;

public class IndexModel(IPortalStore store, IAuthorizationService auth, IOptions<PortalOptions> options) : PortalPageModel
{
    [BindProperty(SupportsGet = true)] public string? Quarter { get; set; }

    public IReadOnlyList<StatementRow> Statements { get; private set; } = [];
    public bool CanRun { get; private set; }
    public string DefaultQuarter => Periods.PreviousQuarter(DateTime.Today);
    public int DefaultPeriod => Periods.FromDate(DateTime.Today.AddMonths(-1));

    public async Task OnGetAsync()
    {
        Statements = await store.ListStatementsAsync(Periods.IsQuarter(Quarter) ? Quarter : null);
        CanRun = (await auth.AuthorizeAsync(User, Policies.Approver)).Succeeded;
    }

    public async Task<IActionResult> OnPostAccrualsAsync(string? period)
    {
        if (!(await auth.AuthorizeAsync(User, Policies.Approver)).Succeeded)
            return Forbid();
        if (!Periods.TryParse(period, out int p))
        {
            TempData["Error"] = "Period must be yyyyMM.";
            return RedirectToPage();
        }
        return await TryAsync(async () =>
        {
            await store.CalculateAccrualsAsync(p);
            TempData["Message"] = $"Accruals for {Periods.Format(p)} recalculated.";
            return RedirectToPage();
        }, () => RedirectToPage());
    }

    public async Task<IActionResult> OnPostRequestAsync(string? quarter)
    {
        if (!(await auth.AuthorizeAsync(User, Policies.Approver)).Succeeded)
            return Forbid();
        if (!Periods.IsQuarter(quarter))
        {
            TempData["Error"] = "Quarter must be yyyyQn, e.g. 2026Q3.";
            return RedirectToPage();
        }
        return await TryAsync(async () =>
        {
            var requested = await store.RequestStatementsAsync(quarter!, Actor, null);
            TempData["Message"] = requested.Count == 0
                ? $"Nothing to state for {quarter}."
                : $"{requested.Count} statement(s) requested for {quarter}: " + string.Join(", ", requested.Select(r => $"{r.Code} {r.Amount:N2}"));
            return RedirectToPage(new { quarter });
        }, () => RedirectToPage());
    }

    public async Task<IActionResult> OnGetFileAsync(int id)
    {
        StatementDocument? doc = await store.GetStatementAsync(id);
        string? path = doc?.Header.FilePath;
        if (path is null)
            return NotFound();

        string root = Path.GetFullPath(options.Value.StatementRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(path);
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(full))
            return NotFound();

        return PhysicalFile(full, "text/html; charset=utf-8");
    }
}
