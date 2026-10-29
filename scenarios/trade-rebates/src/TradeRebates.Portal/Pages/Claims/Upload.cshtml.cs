using ContosoFoods.TradeRebates.Core;
using ContosoFoods.TradeRebates.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace ContosoFoods.TradeRebates.Portal.Pages.Claims;

public class UploadModel(IPortalStore store, IOptions<PortalOptions> options) : PortalPageModel
{
    public IReadOnlyList<WorkbookImportResult>? Results { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(IFormFile? workbook)
    {
        if (workbook is null || workbook.Length == 0)
        {
            TempData["Error"] = "Choose a workbook to upload.";
            return RedirectToPage();
        }
        if (!string.Equals(Path.GetExtension(workbook.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Only .xlsx workbooks can be imported.";
            return RedirectToPage();
        }
        if (workbook.Length > options.Value.MaxUploadBytes)
        {
            TempData["Error"] = "The workbook is too large.";
            return RedirectToPage();
        }

        // The database server reads the workbook itself, so it is saved to the share first.
        string user = Actor.Contains('\\') ? Actor[(Actor.LastIndexOf('\\') + 1)..] : Actor;
        string name = $"{DateTime.Now:yyyyMMdd-HHmmss}-{Sanitize(user)}-{Sanitize(Path.GetFileNameWithoutExtension(workbook.FileName))}.xlsx";
        string path = Path.Combine(options.Value.WorkbookUploadFolder, name);
        Directory.CreateDirectory(options.Value.WorkbookUploadFolder);
        await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            await workbook.CopyToAsync(target);

        try
        {
            return await TryAsync(async () =>
            {
                Results = await store.ImportWorkbookAsync(path, Actor);
                return Page();
            }, () => RedirectToPage());
        }
        catch (SqlException ex)
        {
            // Typically the sheet or a column is missing, or the file is not a real .xlsx.
            TempData["Error"] = "The workbook could not be read: " + ex.Message;
            return RedirectToPage();
        }
    }

    private static string Sanitize(string s)
    {
        var chars = s.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').Take(40).ToArray();
        return chars.Length == 0 ? "workbook" : new string(chars);
    }
}
