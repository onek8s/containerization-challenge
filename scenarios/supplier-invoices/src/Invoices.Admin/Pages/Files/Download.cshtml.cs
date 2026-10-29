using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.StaticFiles;
using Wingtip.Invoices.Admin.Services;

namespace Wingtip.Invoices.Admin.Pages.Files;

/// <summary>Streams the archived original from the archive share.</summary>
public class DownloadModel : PageModel
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();
    private readonly HubData data;
    private readonly ILogger<DownloadModel> logger;

    public DownloadModel(HubData data, ILogger<DownloadModel> logger)
    {
        this.data = data;
        this.logger = logger;
    }

    public async Task<IActionResult> OnGetAsync(long id)
    {
        var file = await data.FileAsync(id);
        if (file?.ArchivePath == null) return NotFound();

        try
        {
            var stream = new FileStream(file.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (!ContentTypes.TryGetContentType(file.FileName, out var contentType)) contentType = "text/plain";
            return File(stream, contentType, file.FileName);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Archived file {Path} cannot be read", file.ArchivePath);
            TempData["Error"] = "The archived file is not available: " + ex.Message;
            return RedirectToPage("/Files/Index", new { fileId = id });
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "No access to archived file {Path}", file.ArchivePath);
            TempData["Error"] = "The application has no access to the archive share.";
            return RedirectToPage("/Files/Index", new { fileId = id });
        }
    }
}
