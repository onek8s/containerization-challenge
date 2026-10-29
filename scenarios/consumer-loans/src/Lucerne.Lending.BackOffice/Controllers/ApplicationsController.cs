using Lucerne.Lending.BackOffice.Infrastructure;
using Lucerne.Lending.BackOffice.Models;
using Lucerne.Lending.CoreBanking;
using Lucerne.Lending.Data;
using Lucerne.Lending.Domain;
using Lucerne.Lending.Workflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lucerne.Lending.BackOffice.Controllers;

public sealed class ApplicationsController : Controller
{
    private const int PageSize = 25;
    private readonly ApplicationRepository _applications;
    private readonly DecisionRepository _decisions;
    private readonly DocumentRepository _documents;
    private readonly ProductRepository _products;
    private readonly ICoreCustomerService _customers;
    private readonly UnderwritingService _underwriting;
    private readonly BookingService _booking;
    private readonly ILogger<ApplicationsController> _log;

    public ApplicationsController(ApplicationRepository applications, DecisionRepository decisions, DocumentRepository documents,
        ProductRepository products, ICoreCustomerService customers, UnderwritingService underwriting, BookingService booking,
        ILogger<ApplicationsController> log)
    {
        _applications = applications;
        _decisions = decisions;
        _documents = documents;
        _products = products;
        _customers = customers;
        _underwriting = underwriting;
        _booking = booking;
        _log = log;
    }

    public async Task<IActionResult> Index(string queue = "referred", string? q = null, int page = 0, CancellationToken ct = default)
    {
        ApplicationStatus[]? statuses = queue switch
        {
            "referred" => new[] { ApplicationStatus.Referred },
            "booking" => new[] { ApplicationStatus.Signed, ApplicationStatus.Booked },
            "inprogress" => new[] { ApplicationStatus.Submitted, ApplicationStatus.Scoring, ApplicationStatus.Failed, ApplicationStatus.Approved,
                ApplicationStatus.OfferIssued, ApplicationStatus.OfferAccepted, ApplicationStatus.AwaitingSignature },
            "closed" => new[] { ApplicationStatus.Disbursed, ApplicationStatus.Declined, ApplicationStatus.Cancelled, ApplicationStatus.Expired },
            _ => null
        };
        var items = await _applications.SearchAsync(statuses, queue == "mine" ? User.UserName() : null, q, page * PageSize, PageSize, ct);
        return View(new QueueModel { Queue = queue, Search = q, Page = page, Items = items });
    }

    public async Task<IActionResult> Details(string id, CancellationToken ct)
    {
        var app = await _applications.GetAsync(id, ct);
        if (app == null) return NotFound();
        var customers = new List<CustomerView>();
        foreach (var a in app.Applicants)
        {
            try
            {
                var core = await _customers.FindByPersonalIdentityCodeAsync(a.PersonalIdentityCode, ct);
                customers.Add(new CustomerView
                {
                    Applicant = a,
                    Core = core,
                    Exposure = core == null ? null : await _customers.GetExposureAsync(core.CustomerNumber, ct),
                    Accounts = core == null ? Array.Empty<CoreAccount>() : await _customers.GetAccountsAsync(core.CustomerNumber, ct)
                });
            }
            catch (CoreBankingException ex)
            {
                _log.LogWarning(ex, "Core inquiry failed for {Reference}", id);
                customers.Add(new CustomerView { Applicant = a, CoreError = ex.Message });
            }
        }
        var status = app.Row.StatusValue;
        return View(new ApplicationDetailsModel
        {
            Application = app,
            Product = (await _products.GetAsync(app.Row.ProductCode, ct))!,
            Customers = customers,
            Decisions = await _decisions.ListAsync(app.Row.Id, ct),
            BureauReports = await _decisions.BureauReportsAsync(app.Row.Id, ct),
            Offer = await _decisions.CurrentOfferAsync(app.Row.Id, ct),
            Documents = await _documents.ListForApplicationAsync(app.Row.Id, ct),
            Notes = await _applications.NotesAsync(app.Row.Id, ct),
            Audit = await _applications.AuditTrailAsync(app.Row.Id, ct),
            CanDecide = status == ApplicationStatus.Referred && (User.IsInRole(Roles.Underwriter) || User.IsInRole(Roles.SeniorUnderwriter)),
            CanBook = status is ApplicationStatus.Signed or ApplicationStatus.Booked && User.IsInRole(Roles.Operations)
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "Underwriting")]
    public async Task<IActionResult> Assign(string id, CancellationToken ct)
    {
        var row = await _applications.GetRowAsync(id, ct);
        if (row == null) return NotFound();
        await _applications.AssignAsync(row.Id, User.UserName(), ct);
        await _applications.AuditAsync(row.Id, "Assigned", User.UserName(), null);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "Underwriting")]
    public async Task<IActionResult> Decide(DecideForm form, CancellationToken ct)
    {
        try
        {
            await _underwriting.DecideAsync(form.Reference, new ManualDecision(form.Approve, form.Grade, form.Amount, form.TermMonths,
                form.MarginOverridePercent, form.Comment), User.UserName(), User.IsInRole(Roles.SeniorUnderwriter), form.Version, ct);
            TempData["Message"] = form.Approve ? "Application approved, offer sent to the customer." : "Application declined.";
        }
        catch (Exception ex) when (ex is ValidationFailedException or ConflictException or UnauthorizedAccessException)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id = form.Reference });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "Operations")]
    public async Task<IActionResult> Book(string id, CancellationToken ct)
    {
        var outcome = await _booking.BookAsync(id, User.UserName(), ct);
        TempData[outcome.Success ? "Message" : "Error"] = outcome.Success ? $"Booked to core account {outcome.AccountNumber}. {outcome.Message}" : outcome.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Note(string id, string text, CancellationToken ct)
    {
        var row = await _applications.GetRowAsync(id, ct);
        if (row == null) return NotFound();
        if (!string.IsNullOrWhiteSpace(text)) await _applications.AddNoteAsync(row.Id, User.UserName(), text.Trim(), ct);
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Document(long id, CancellationToken ct)
    {
        var doc = await _documents.GetAsync(id, ct);
        return doc?.Content == null ? NotFound() : File(doc.Content, doc.ContentType, doc.FileName);
    }
}
