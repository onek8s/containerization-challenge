using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Data.Queries;
using Fabrikam.Housing.Documents;
using Fabrikam.Housing.Integration.Payments;
using Fabrikam.Housing.Rent;
using Fabrikam.Housing.Tenancies;
using Fabrikam.Housing.TenantPortal.Models;
using Fabrikam.Housing.TenantPortal.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Fabrikam.Housing.TenantPortal.Controllers
{
    public class DirectDebitController : Controller
    {
        private const string PendingKey = "dd-pending";
        private readonly CurrentTenant _current;
        private readonly HousingDbContext _db;
        private readonly RentAccountQueries _rent;
        private readonly IPaymentsGateway _payments;
        private readonly CollectionSchedule _schedule;
        private readonly ILetterComposer _letters;
        private readonly TenantNotifier _notifier;
        private readonly AuditWriter _audit;
        private readonly IMapper _mapper;
        private readonly IClock _clock;
        private readonly ILogger<DirectDebitController> _logger;

        public DirectDebitController(CurrentTenant current, HousingDbContext db, RentAccountQueries rent, IPaymentsGateway payments,
            CollectionSchedule schedule, ILetterComposer letters, TenantNotifier notifier, AuditWriter audit, IMapper mapper, IClock clock,
            ILogger<DirectDebitController> logger)
        {
            _current = current;
            _db = db;
            _rent = rent;
            _payments = payments;
            _schedule = schedule;
            _letters = letters;
            _notifier = notifier;
            _audit = audit;
            _mapper = mapper;
            _clock = clock;
            _logger = logger;
        }

        private class PendingSetup
        {
            public string FlowId { get; set; }
            public CollectionFrequency Frequency { get; set; }
            public int CollectionDay { get; set; }
            public decimal Amount { get; set; }
        }

        private Task<DirectDebitMandate> CurrentMandateAsync(int tenancyId) =>
            _db.Mandates.Where(m => m.TenancyId == tenancyId && (m.Status == MandateStatus.Active || m.Status == MandateStatus.PendingSubmission
                || m.Status == MandateStatus.Submitted || m.Status == MandateStatus.PendingCustomerApproval))
                .OrderByDescending(m => m.Id).FirstOrDefaultAsync();

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var tenancy = await _current.TenancyAsync();
            var model = await BuildAsync(tenancy, new DirectDebitViewModel());
            model.Amount = model.SuggestedMonthly;
            if (model.Current != null)
            {
                model.Frequency = model.Current.Frequency;
                model.CollectionDay = model.Current.CollectionDay;
                model.Amount = model.Current.Amount;
            }
            return View(model);
        }

        private async Task<DirectDebitViewModel> BuildAsync(Tenancy tenancy, DirectDebitViewModel model)
        {
            var mandate = await CurrentMandateAsync(tenancy.Id);
            model.Current = mandate == null ? null : _mapper.Map<MandateViewModel>(mandate);
            model.WeeklyCharge = tenancy.WeeklyCharge;
            model.Balance = await _rent.BalanceAsync(tenancy.Id);
            model.SuggestedWeekly = CollectionSchedule.SuggestedAmount(tenancy.WeeklyCharge, CollectionFrequency.Weekly);
            model.SuggestedFortnightly = CollectionSchedule.SuggestedAmount(tenancy.WeeklyCharge, CollectionFrequency.Fortnightly);
            model.SuggestedFourWeekly = CollectionSchedule.SuggestedAmount(tenancy.WeeklyCharge, CollectionFrequency.FourWeekly);
            model.SuggestedMonthly = CollectionSchedule.SuggestedAmount(tenancy.WeeklyCharge, CollectionFrequency.Monthly);
            return model;
        }

        [HttpPost]
        public async Task<IActionResult> Index(DirectDebitViewModel model)
        {
            var tenant = await _current.GetAsync();
            try
            {
                CollectionSchedule.Validate(model.Frequency, model.CollectionDay);
            }
            catch (BusinessRuleException ex)
            {
                ModelState.AddModelError(nameof(model.CollectionDay), ex.Message);
            }
            if (model.Amount.HasValue && model.Amount.Value < CollectionSchedule.SuggestedAmount(tenant.Tenancy.WeeklyCharge, model.Frequency) * 0.9m)
                ModelState.AddModelError(nameof(model.Amount), "The amount must cover your rent for the period. Call us if you want to agree a lower amount.");
            if (!ModelState.IsValid) return View(await BuildAsync(tenant.Tenancy, model));

            // The bank details are collected on the Paystream page; we only keep the mandate reference.
            HttpContext.Session.SetString("dd-started", _clock.Now.ToString("O"));
            var successUrl = Url.Action(nameof(Complete), "DirectDebit", null, Request.Scheme);
            RedirectFlow flow;
            try
            {
                flow = await _payments.StartMandateAsync(tenant.Tenancy.Reference, "Fabrikam Homes rent " + tenant.Tenancy.Reference, tenant.Email, successUrl, HttpContext.Session.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not start direct debit set-up for {Tenancy}", tenant.Tenancy.Reference);
                ModelState.AddModelError("", "Direct debit set-up is not available at the moment. Please try again later.");
                return View(await BuildAsync(tenant.Tenancy, model));
            }

            HttpContext.Session.SetString(PendingKey, JsonConvert.SerializeObject(new PendingSetup
            {
                FlowId = flow.FlowId,
                Frequency = model.Frequency,
                CollectionDay = model.CollectionDay,
                Amount = Money.Round(model.Amount.Value)
            }));
            return Redirect(flow.RedirectUrl);
        }

        [HttpGet]
        public async Task<IActionResult> Complete(string redirect_flow_id)
        {
            var tenant = await _current.GetAsync();
            var json = HttpContext.Session.GetString(PendingKey);
            if (json == null) return RedirectToAction(nameof(Index));
            var pending = JsonConvert.DeserializeObject<PendingSetup>(json);
            if (!string.IsNullOrEmpty(redirect_flow_id) && redirect_flow_id != pending.FlowId) return RedirectToAction(nameof(Index));

            CompletedMandate completed;
            try
            {
                completed = await _payments.CompleteMandateAsync(pending.FlowId, HttpContext.Session.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Completing direct debit flow {Flow} failed", pending.FlowId);
                TempData["Error"] = "We could not complete your direct debit. Your bank account has not been charged. Please try again.";
                return RedirectToAction(nameof(Index));
            }

            var previous = await CurrentMandateAsync(tenant.TenancyId);
            if (previous != null && previous.ProviderMandateId != completed.MandateId)
            {
                try
                {
                    await _payments.CancelMandateAsync(previous.ProviderMandateId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Cancelling replaced mandate {Mandate} failed; income team to cancel manually", previous.ProviderMandateId);
                }
                previous.Status = MandateStatus.Cancelled;
                previous.CancelledAt = _clock.Now;
                previous.CancellationReason = "Replaced by tenant on portal";
                previous.ModifiedAt = _clock.Now;
                previous.ModifiedBy = User.Identity.Name;
            }

            var first = _schedule.FirstCollection(pending.Frequency, pending.CollectionDay, _clock.Today);
            var mandate = new DirectDebitMandate
            {
                TenancyId = tenant.TenancyId,
                ProviderCustomerId = completed.CustomerId,
                ProviderMandateId = completed.MandateId,
                BankReference = ("FABRIKAM" + tenant.Tenancy.Reference).ToUpperInvariant(),
                AccountHolderName = completed.AccountHolderName,
                AccountNumberEnding = completed.AccountNumberEnding,
                Status = MandateStatus.PendingSubmission,
                Frequency = pending.Frequency,
                CollectionDay = pending.CollectionDay,
                Amount = pending.Amount,
                FirstCollectionDate = first,
                NextCollectionDate = first,
                CreatedAt = _clock.Now,
                CreatedBy = User.Identity.Name
            };
            _db.Mandates.Add(mandate);
            await _db.SaveChangesAsync();
            HttpContext.Session.Remove(PendingKey);
            await _audit.WriteAsync(User.Identity.Name, "Tenancy", tenant.Tenancy.Reference, "DirectDebitSetUp",
                new { mandate.ProviderMandateId, mandate.Frequency, mandate.CollectionDay, mandate.Amount, first });

            var view = _mapper.Map<MandateViewModel>(mandate);
            await _notifier.EmailAsync(tenant, "DD_CONFIRMATION", "Your Fabrikam Homes direct debit", ConfirmationHtml(tenant, mandate, view, completed.BankName));
            TempData["Message"] = $"Your direct debit is set up. The first payment of {Money.Format(mandate.Amount)} will be taken on or just after {first:d MMMM yyyy}.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// The Direct Debit Guarantee requires written confirmation (advance notice) at least three working days
        /// before the first collection.
        /// </summary>
        private string ConfirmationHtml(Tenant tenant, DirectDebitMandate mandate, MandateViewModel view, string bankName)
        {
            var fields = new Dictionary<string, string>
            {
                ["FirstName"] = tenant.FirstName,
                ["TenancyReference"] = tenant.Tenancy.Reference,
                ["BankReference"] = mandate.BankReference,
                ["AccountHolder"] = mandate.AccountHolderName,
                ["AccountEnding"] = mandate.AccountNumberEnding,
                ["BankName"] = bankName,
                ["Amount"] = Money.Format(mandate.Amount),
                ["Schedule"] = view.Schedule,
                ["FirstCollection"] = mandate.FirstCollectionDate.ToString("d MMMM yyyy")
            };
            try
            {
                return _letters.Compose(new LetterModel
                {
                    TemplateCode = "DirectDebitConfirmation",
                    RecipientName = tenant.FullName,
                    AddressLines = new[] { tenant.Tenancy.Property.AddressLine1, tenant.Tenancy.Property.AddressLine2, tenant.Tenancy.Property.Town, tenant.Tenancy.Property.Postcode }
                        .Where(l => !string.IsNullOrEmpty(l)).ToList(),
                    Date = _clock.Today,
                    Reference = tenant.Tenancy.Reference,
                    LargePrint = tenant.NeedsLargePrint,
                    Fields = fields
                });
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                _logger.LogWarning("Letter template DirectDebitConfirmation not found, using the plain confirmation");
                return $"<p>Dear {WebUtility.HtmlEncode(tenant.FirstName)},</p>" +
                       $"<p>Thank you for setting up a direct debit for your rent (tenancy {tenant.Tenancy.Reference}).</p>" +
                       "<table>" +
                       $"<tr><td>Account holder</td><td>{WebUtility.HtmlEncode(mandate.AccountHolderName)}</td></tr>" +
                       $"<tr><td>Account number ending</td><td>{mandate.AccountNumberEnding}</td></tr>" +
                       $"<tr><td>Reference on your bank statement</td><td>{mandate.BankReference}</td></tr>" +
                       $"<tr><td>Amount</td><td>{fields["Amount"]} ({view.Schedule})</td></tr>" +
                       $"<tr><td>First collection</td><td>{fields["FirstCollection"]}</td></tr>" +
                       "</table><p>Your payments are protected by the Direct Debit Guarantee.</p><p>Fabrikam Homes, Income Team</p>";
            }
        }

        [HttpPost]
        public async Task<IActionResult> Cancel()
        {
            var tenant = await _current.GetAsync();
            var mandate = await CurrentMandateAsync(tenant.TenancyId);
            if (mandate == null) return RedirectToAction(nameof(Index));
            try
            {
                await _payments.CancelMandateAsync(mandate.ProviderMandateId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cancelling mandate {Mandate} failed", mandate.ProviderMandateId);
                TempData["Error"] = "We could not cancel your direct debit just now. Please try again or call 0300 123 4567.";
                return RedirectToAction(nameof(Index));
            }
            mandate.Status = MandateStatus.Cancelled;
            mandate.CancelledAt = _clock.Now;
            mandate.CancellationReason = "Cancelled by tenant on portal";
            mandate.ModifiedAt = _clock.Now;
            mandate.ModifiedBy = User.Identity.Name;
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(User.Identity.Name, "Tenancy", tenant.Tenancy.Reference, "DirectDebitCancelled", new { mandate.ProviderMandateId });
            TempData["Message"] = "Your direct debit has been cancelled. Please make sure you pay your rent another way.";
            return RedirectToAction(nameof(Index));
        }
    }
}
