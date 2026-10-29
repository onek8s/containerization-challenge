using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Wingtip.Invoices.Admin.Services;

namespace Wingtip.Invoices.Admin.Pages.Files;

public class IndexModel : PageModel
{
    private readonly HubData data;
    private readonly ProcessorClient processor;
    private readonly CurrentUser user;
    private readonly IAuthorizationService authorization;

    public IndexModel(HubData data, ProcessorClient processor, CurrentUser user, IAuthorizationService authorization)
    {
        this.data = data;
        this.processor = processor;
        this.user = user;
        this.authorization = authorization;
    }

    [BindProperty(SupportsGet = true)]
    public string Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? SupplierId { get; set; }

    [BindProperty(SupportsGet = true)]
    public long? FileId { get; set; }

    public IList<FileRow> Files { get; private set; }
    public IList<SupplierRow> Suppliers { get; private set; }
    public bool IsAdmin { get; private set; }

    public async Task OnGetAsync()
    {
        Suppliers = await data.SuppliersAsync();
        IsAdmin = await user.IsAdminAsync();
        if (FileId.HasValue)
        {
            var file = await data.FileAsync(FileId.Value);
            Files = file == null ? new List<FileRow>() : new List<FileRow> { file };
        }
        else
        {
            Files = await data.FilesAsync(string.IsNullOrEmpty(Status) ? null : Status, SupplierId);
        }
    }

    public async Task<IActionResult> OnPostResubmitAsync(long fileId)
    {
        if (!(await authorization.AuthorizeAsync(User, Policies.Admin)).Succeeded) return Forbid();
        try
        {
            await processor.ResubmitFileAsync(fileId, user.Account);
            TempData["Message"] = $"File {fileId} was queued again.";
        }
        catch (ProcessorException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage(new { fileId });
    }
}
