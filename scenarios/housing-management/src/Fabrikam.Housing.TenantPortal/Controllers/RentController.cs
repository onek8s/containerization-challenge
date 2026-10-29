using System;
using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Data.Queries;
using Fabrikam.Housing.Documents;
using Fabrikam.Housing.Integration.Payments;
using Fabrikam.Housing.Rent;
using Fabrikam.Housing.TenantPortal.Models;
using Fabrikam.Housing.TenantPortal.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fabrikam.Housing.TenantPortal.Controllers
{
    public class RentController : Controller
    {
        private const string CardSessionKey = "card-session";
        private readonly CurrentTenant _current;
        private readonly HousingDbContext _db;
        private readonly RentAccountQueries _rent;
        private readonly IStatementPdfWriter _pdf;
        private readonly IPaymentsGateway _payments;
        private readonly AuditWriter _audit;
        private readonly TenantNotifier _notifier;
        private readonly IClock _clock;
        private readonly ILogger<RentController> _logger;

        public RentController(CurrentTenant current, HousingDbContext db, RentAccountQueries rent, IStatementPdfWriter pdf,
            IPaymentsGateway payments, AuditWriter audit, TenantNotifier notifier, IClock clock, ILogger<RentController> logger)
        {
            _current = current;
            _db = db;
            _rent = rent;
            _pdf = pdf;
            _payments = payments;
            _audit = audit;
            _notifier = notifier;
            _clock = clock;
            _logger = logger;
        }

        public async Task<IActionResult> Statement(DateTime? from, DateTime? to)
        {
            var tenancy = await _current.TenancyAsync();
            var (start, end) = Range(from, to);
            return View(new StatementViewModel
            {
                From = start,
                To = end,
                TenancyReference = tenancy.Reference,
                Lines = await _rent.StatementAsync(tenancy.Id, start, end),
                Balance = await _rent.BalanceAsync(tenancy.Id)
            });
        }

        public async Task<IActionResult> StatementPdf(DateTime? from, DateTime? to)
        {
            var tenant = await _current.GetAsync();
            var (start, end) = Range(from, to);
            var lines = await _rent.StatementAsync(tenant.TenancyId, start, end);
            var header = new StatementHeader
            {
                TenancyReference = tenant.Tenancy.Reference,
                TenantName = tenant.FullName,
                Address = tenant.Tenancy.Property.SingleLineAddress,
                From = start,
                To = end,
                WeeklyCharge = tenant.Tenancy.WeeklyCharge
            };
            var bytes = _pdf.Write(header, lines);
            return File(bytes, "application/pdf", $"Rent statement {tenant.Tenancy.Reference} {end:yyyy-MM-dd}.pdf");
        }

        private (DateTime, DateTime) Range(DateTime? from, DateTime? to)
        {
            var end = (to ?? _clock.Today).Date;
            var start = (from ?? end.AddMonths(-3)).Date;
            if (start > end) start = end.AddMonths(-3);
            if ((end - start).TotalDays > 366 * 3) start = end.AddYears(-3);
            return (start, end);
        }

        [HttpGet]
        public async Task<IActionResult> Pay()
        {
            var tenancy = await _current.TenancyAsync();
            var balance = await _rent.BalanceAsync(tenancy.Id);
            return View(new PayViewModel { Balance = balance, WeeklyCharge = tenancy.WeeklyCharge, Amount = balance > 0 ? balance : tenancy.WeeklyCharge });
        }

        [HttpPost]
        public async Task<IActionResult> Pay(PayViewModel model)
        {
            var tenant = await _current.GetAsync();
            if (!ModelState.IsValid)
            {
                model.Balance = await _rent.BalanceAsync(tenant.TenancyId);
                model.WeeklyCharge = tenant.Tenancy.WeeklyCharge;
                return View(model);
            }

            var returnUrl = Url.Action(nameof(PayReturn), "Rent", null, Request.Scheme);
            CardPaymentSession session;
            try
            {
                session = await _payments.StartCardPaymentAsync(tenant.Tenancy.Reference, Money.Round(model.Amount.Value), returnUrl, tenant.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not start card payment for {Tenancy}", tenant.Tenancy.Reference);
                ModelState.AddModelError("", "Card payments are not available at the moment. Please try again later or call 0300 123 4567.");
                model.Balance = await _rent.BalanceAsync(tenant.TenancyId);
                model.WeeklyCharge = tenant.Tenancy.WeeklyCharge;
                return View(model);
            }
            HttpContext.Session.SetString(CardSessionKey, session.SessionId);
            return Redirect(session.PaymentUrl);
        }

        [HttpGet]
        public async Task<IActionResult> PayReturn(string session_id)
        {
            var tenant = await _current.GetAsync();
            var sessionId = HttpContext.Session.GetString(CardSessionKey) ?? session_id;
            if (string.IsNullOrEmpty(sessionId)) return RedirectToAction(nameof(Pay));

            var result = await _payments.GetCardPaymentAsync(sessionId);
            if (result == null || !string.Equals(result.Reference, tenant.Tenancy.Reference, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Card session {Session} does not belong to tenancy {Tenancy}", sessionId, tenant.Tenancy.Reference);
                return RedirectToAction(nameof(Pay));
            }

            var model = new PayResultViewModel { Amount = result.Amount, PaymentReference = result.PaymentId, Status = result.Status, Succeeded = result.Status == "succeeded" };
            if (model.Succeeded)
            {
                // The Paystream webhook may already have posted the payment; the reference keeps it to one line.
                var exists = await _db.RentTransactions.AnyAsync(t => t.TenancyId == tenant.TenancyId && t.Reference == result.PaymentId && t.Type == TransactionType.CardPayment);
                if (!exists)
                {
                    var postedOn = result.CompletedAt ?? _clock.Now;
                    _db.RentTransactions.Add(new RentTransaction
                    {
                        TenancyId = tenant.TenancyId,
                        PostedOn = postedOn,
                        RentWeek = RentCalendar.WeekStart(postedOn),
                        Type = TransactionType.CardPayment,
                        Amount = -Money.Round(result.Amount),
                        Reference = result.PaymentId,
                        Description = "Card payment (online)",
                        Source = "Portal"
                    });
                    await _db.SaveChangesAsync();
                    await _audit.WriteAsync(User.Identity.Name, "Tenancy", tenant.Tenancy.Reference, "CardPayment", new { result.PaymentId, result.Amount });
                    await _notifier.EmailAsync(tenant, "CARD_RECEIPT", "Your Fabrikam Homes rent payment",
                        $"<p>Dear {System.Net.WebUtility.HtmlEncode(tenant.FirstName)},</p><p>Thank you. We have received your payment of {Money.Format(result.Amount)} " +
                        $"for tenancy {tenant.Tenancy.Reference}. Payment reference: {result.PaymentId}.</p><p>Fabrikam Homes</p>");
                }
                HttpContext.Session.Remove(CardSessionKey);
            }
            return View("PayResult", model);
        }
    }
}
