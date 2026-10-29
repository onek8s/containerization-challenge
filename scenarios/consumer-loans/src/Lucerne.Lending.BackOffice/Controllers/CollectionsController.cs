using Lucerne.Lending.BackOffice.Infrastructure;
using Lucerne.Lending.BackOffice.Models;
using Lucerne.Lending.CoreBanking;
using Lucerne.Lending.Data;
using Lucerne.Lending.Domain;
using Lucerne.Lending.Workflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lucerne.Lending.BackOffice.Controllers;

public sealed class CollectionsController : Controller
{
    private readonly CollectionRepository _cases;
    private readonly DocumentRepository _documents;
    private readonly ICoreLoanService _loans;
    private readonly ICoreCustomerService _customers;
    private readonly CollectionsService _collections;

    public CollectionsController(CollectionRepository cases, DocumentRepository documents, ICoreLoanService loans, ICoreCustomerService customers,
        CollectionsService collections)
    {
        _cases = cases;
        _documents = documents;
        _loans = loans;
        _customers = customers;
        _collections = collections;
    }

    public async Task<IActionResult> Index(CollectionStatus? status, CancellationToken ct) =>
        View(await _cases.ListAsync(status, null, ct));

    public async Task<IActionResult> Details(long id, CancellationToken ct)
    {
        var c = await _cases.GetAsync(id, ct);
        if (c == null) return NotFound();
        CoreLoanSummary? loan = null;
        CoreCustomer? customer = null;
        IReadOnlyList<CorePayment> payments = Array.Empty<CorePayment>();
        string? error = null;
        try
        {
            loan = await _loans.GetLoanAsync(c.AccountNumber, ct);
            customer = await _customers.GetAsync(c.CustomerNumber, ct);
            payments = await _loans.GetPaymentHistoryAsync(c.AccountNumber, DateOnly.FromDateTime(DateTime.Today.AddMonths(-12)), ct);
        }
        catch (CoreBankingException ex)
        {
            error = ex.Message;
        }
        return View(new CollectionDetailsModel
        {
            Case = c,
            Loan = loan,
            Customer = customer,
            Payments = payments,
            Actions = await _cases.ActionsAsync(id, ct),
            Plans = await _cases.PlansAsync(id, ct),
            Documents = await _documents.ListForCaseAsync(id, ct),
            CoreError = error
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "Collections")]
    public async Task<IActionResult> Promise(long id, DateTime date, decimal amount, CancellationToken ct)
    {
        try
        {
            await _collections.RecordPromiseAsync(id, DateOnly.FromDateTime(date), amount, User.UserName(), ct);
            TempData["Message"] = "Promise to pay recorded.";
        }
        catch (ValidationFailedException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "Collections")]
    public async Task<IActionResult> Plan(long id, int months, CancellationToken ct)
    {
        try
        {
            var plan = await _collections.CreatePaymentPlanAsync(id, months, User.UserName(), ct);
            TempData["Message"] = $"Payment plan for {plan.Months} months registered in the core.";
        }
        catch (Exception ex) when (ex is ConflictException or ArgumentOutOfRangeException or CoreBankingException)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "Collections")]
    public async Task<IActionResult> Assign(long id)
    {
        await _cases.AssignAsync(id, User.UserName());
        return RedirectToAction(nameof(Details), new { id });
    }
}
