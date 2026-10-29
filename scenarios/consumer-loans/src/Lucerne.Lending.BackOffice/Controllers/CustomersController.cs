using Lucerne.Lending.BackOffice.Models;
using Lucerne.Lending.CoreBanking;
using Lucerne.Lending.Data;
using Lucerne.Lending.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Lucerne.Lending.BackOffice.Controllers;

public sealed class CustomersController : Controller
{
    private readonly ICoreCustomerService _customers;
    private readonly ApplicationRepository _applications;

    public CustomersController(ICoreCustomerService customers, ApplicationRepository applications)
    {
        _customers = customers;
        _applications = applications;
    }

    public async Task<IActionResult> Index(string? pic, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(pic)) return View(new CustomerSearchModel());
        pic = pic.Trim().ToUpperInvariant();
        if (!PersonalIdentityCode.IsValid(pic)) return View(new CustomerSearchModel { Pic = pic, Message = "Personal identity code is not valid." });
        var customer = await _customers.FindByPersonalIdentityCodeAsync(pic, ct);
        return View(new CustomerSearchModel
        {
            Pic = pic,
            Customer = customer,
            Exposure = customer == null ? null : await _customers.GetExposureAsync(customer.CustomerNumber, ct),
            Accounts = customer == null ? Array.Empty<CoreAccount>() : await _customers.GetAccountsAsync(customer.CustomerNumber, ct),
            Applications = await _applications.ListForPersonAsync(pic, ct),
            Message = customer == null ? "Not a customer in the core system." : null
        });
    }
}
