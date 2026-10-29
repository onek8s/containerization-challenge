using Lucerne.Lending.Contracts;
using Lucerne.Lending.CoreBanking;
using Lucerne.Lending.Data;
using Lucerne.Lending.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lucerne.Lending.Api.Controllers;

/// <summary>Customer's loans from the core ledger for the "My loans" page.</summary>
[ApiController]
[Route("api/customers/{pic}")]
[Authorize(Policy = "Customer")]
public sealed class CustomersController : ControllerBase
{
    private readonly ICoreCustomerService _customers;
    private readonly ICoreLoanService _loans;
    private readonly ProductRepository _products;

    public CustomersController(ICoreCustomerService customers, ICoreLoanService loans, ProductRepository products)
    {
        _customers = customers;
        _loans = loans;
        _products = products;
    }

    [HttpGet("loans")]
    public async Task<ActionResult<IEnumerable<CustomerLoanDto>>> Loans(string pic, CancellationToken ct)
    {
        if (!PersonalIdentityCode.IsValid(pic)) return BadRequest();
        var customer = await _customers.FindByPersonalIdentityCodeAsync(pic, ct);
        if (customer == null) return Array.Empty<CustomerLoanDto>();
        var loans = await _loans.ListLoansAsync(customer.CustomerNumber, ct);
        var names = (await _products.ListAsync(activeOnly: false, ct)).GroupBy(p => p.CoreProductCode).ToDictionary(g => g.Key, g => g.First().Name);
        return loans.Where(l => l.Status != "BOOK").Select(l => new CustomerLoanDto
        {
            AccountNumber = l.AccountNumber,
            ProductName = names.TryGetValue(l.ProductCode, out var name) ? name : l.ProductCode,
            OriginalAmount = l.OriginalAmount,
            Balance = l.Balance,
            NominalRatePercent = l.NominalRatePercent,
            NextDueDate = l.NextDueDate?.ToDateTime(TimeOnly.MinValue),
            NextInstalment = l.NextInstalment,
            Arrears = l.Arrears,
            DaysPastDue = l.DaysPastDue,
            PaymentReference = PaymentReference.ForLoanAccount(l.AccountNumber),
            Status = l.Status
        }).ToList();
    }

    [HttpGet("loans/{account}/payments")]
    public async Task<ActionResult<IEnumerable<PaymentHistoryDto>>> Payments(string pic, string account, CancellationToken ct)
    {
        var customer = await _customers.FindByPersonalIdentityCodeAsync(pic, ct);
        if (customer == null) return NotFound();
        var loans = await _loans.ListLoansAsync(customer.CustomerNumber, ct);
        var names = (await _products.ListAsync(activeOnly: false, ct)).GroupBy(p => p.CoreProductCode).ToDictionary(g => g.Key, g => g.First().Name);
        if (loans.All(l => l.AccountNumber != account)) return NotFound();
        var history = await _loans.GetPaymentHistoryAsync(account, DateOnly.FromDateTime(DateTime.Today.AddYears(-2)), ct);
        return history.Select(h => new PaymentHistoryDto
        {
            BookingDate = h.BookingDate.ToDateTime(TimeOnly.MinValue),
            Type = h.Type,
            Amount = h.Amount,
            Principal = h.Principal,
            Interest = h.Interest,
            Fees = h.Fees,
            BalanceAfter = h.BalanceAfter
        }).ToList();
    }
}
