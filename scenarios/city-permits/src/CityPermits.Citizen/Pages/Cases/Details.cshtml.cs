using System.Security.Cryptography;
using CityPermits.Archive;
using CityPermits.Citizen.Services;
using CityPermits.Data;
using CityPermits.Domain.Model;
using CityPermits.Domain.Workflow;
using CityPermits.Integration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CityPermits.Citizen.Pages.Cases;

public class DetailsModel : PageModel
{
    private static readonly string[] AllowedTypes = { ".pdf", ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".docx", ".dwg" };
    private readonly PermitsDbContext _db;
    private readonly CitizenUser _user;
    private readonly CaseService _cases;
    private readonly IDocumentArchive _archive;
    private readonly PaystreamClient _paystream;

    public DetailsModel(PermitsDbContext db, CitizenUser user, CaseService cases, IDocumentArchive archive, PaystreamClient paystream)
    {
        _db = db; _user = user; _cases = cases; _archive = archive; _paystream = paystream;
    }

    public PermitCase? Case { get; private set; }
    public string? Message { get; private set; }

    private async Task<(Applicant, PermitCase?)> LoadAsync(int id)
    {
        var me = await _user.CurrentAsync(User);
        var c = await _db.Cases.Include(x => x.Fees).Include(x => x.Payments).Include(x => x.Events).Include(x => x.Documents).Include(x => x.Consultations)
            .FirstOrDefaultAsync(x => x.Id == id && x.ApplicantId == me.Id);
        return (me, c);
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        (_, Case) = await LoadAsync(id);
        return Case is null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostUploadAsync(int id, IFormFile? file)
    {
        var (me, c) = await LoadAsync(id);
        if (c is null) return NotFound();
        Case = c;
        if (file is null || file.Length == 0 || file.Length > 25 * 1024 * 1024 || !AllowedTypes.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
        {
            Message = "Upload a PDF, image, Word or DWG file of up to 25 MB.";
            return Page();
        }
        await using var s = file.OpenReadStream();
        var archived = await _archive.StoreAsync(c.Reference, Path.GetFileName(file.FileName), s);
        c.Documents.Add(new CaseDocument
        {
            Kind = DocumentKind.Attachment, FileName = Path.GetFileName(file.FileName), ContentType = file.ContentType, Length = archived.Length,
            Sha256 = archived.Sha256, ArchiveKey = archived.Key, ArchivePath = archived.Path, CreatedUtc = DateTime.UtcNow,
            CreatedBy = "applicant:" + me.Id, VisibleToApplicant = true
        });
        if (c.Status == CaseStatus.InformationRequested)
            CaseWorkflow.Apply(c, CaseWorkflow.InformationProvided, "applicant:" + me.Id, new[] { Roles.Applicant }, $"Uploaded {file.FileName}", DateTime.UtcNow);
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSubmitAsync(int id)
    {
        var (me, c) = await LoadAsync(id);
        if (c is null) return NotFound();
        try
        {
            await _cases.SubmitAsync(c, me);
        }
        catch (WorkflowException ex)
        {
            Case = c;
            Message = ex.Message;
            return Page();
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPayAsync(int id)
    {
        var (me, c) = await LoadAsync(id);
        if (c is null || c.Balance <= 0) return NotFound();
        var session = _paystream.CreateSession(c.Reference, c.Balance, c.Title, me.Email ?? "");
        _db.Payments.Add(new Payment
        {
            CaseId = c.Id, ProviderReference = session.PaymentId, SessionId = session.SessionId, Amount = c.Balance,
            Status = PaymentStatus.Pending, CreatedUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return Redirect(session.RedirectUrl);
    }
}
