using BatchRelease.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BatchRelease.Portal.Pages.Batches;

public class DocumentModel : PageModel
{
    private readonly ISigningHostClient _signing;

    public DocumentModel(ISigningHostClient signing) => _signing = signing;

    public async Task<IActionResult> OnGetAsync(int batchId, int documentId, CancellationToken ct)
    {
        try
        {
            var document = await _signing.OpenDocumentAsync(batchId, documentId, User.Sid(), ct);
            if (document is null)
                return NotFound();
            Response.RegisterForDisposeAsync(document);
            return File(document.Content, document.ContentType, document.FileName);
        }
        catch (SigningHostException ex) when (ex.Status == System.Net.HttpStatusCode.Forbidden)
        {
            return Forbid();
        }
    }
}
