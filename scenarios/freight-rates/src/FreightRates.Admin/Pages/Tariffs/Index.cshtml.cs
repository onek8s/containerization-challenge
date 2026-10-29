using FreightRates.Admin.Services;
using FreightRates.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FreightRates.Admin.Pages.Tariffs;

public class IndexModel : PageModel
{
    private readonly IRatingEngine _engine;
    private readonly TariffUploadService _upload;

    public IndexModel(IRatingEngine engine, TariffUploadService upload)
    {
        _engine = engine;
        _upload = upload;
    }

    public TariffInfo? Tariff { get; private set; }
    public string? TariffError { get; private set; }
    public string? UploadError { get; private set; }

    public Task OnGetAsync(CancellationToken ct) => LoadAsync(ct);

    public async Task<IActionResult> OnPostAsync(List<IFormFile> files, CancellationToken ct)
    {
        if (files.Count == 0)
        {
            UploadError = "Select the files of the tariff release.";
            await LoadAsync(ct);
            return Page();
        }

        try
        {
            var count = await _upload.UploadAsync(files, User.Identity?.Name ?? "", ct);
            TempData["Message"] = $"{count} tariff files sent to the rating host.";
            return RedirectToPage();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            UploadError = ex.Message;
            await LoadAsync(ct);
            return Page();
        }
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        try
        {
            Tariff = await _engine.GetTariffInfoAsync(ct);
        }
        catch (RatingException ex)
        {
            TariffError = ex.Message;
        }
    }
}
