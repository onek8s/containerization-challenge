using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.BackOffice.Infrastructure;
using Fabrikam.Housing.BackOffice.Models;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Data.Queries;
using Fabrikam.Housing.Documents;
using Fabrikam.Housing.Integration.Payments;
using Fabrikam.Housing.Rent;
using Fabrikam.Housing.Tenancies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fabrikam.Housing.BackOffice.Controllers
{
    public class TenanciesController : Controller
    {
        private static readonly TransactionType[] ManualTypes = { TransactionType.Adjustment, TransactionType.WriteOff, TransactionType.Refund, TransactionType.CashPayment, TransactionType.RechargeRepair };

        private readonly HousingDbContext _db;
        private readonly RentAccountQueries _rent;
        private readonly IStatementPdfWriter _statementPdf;
        private readonly IPaymentsGateway _payments;
        private readonly ArrearsPolicy _arrearsPolicy;
        private readonly TenancyLetters _letters;
        private readonly AuditWriter _audit;
        private readonly IClock _clock;
        private readonly ILogger<TenanciesController> _logger;

        public TenanciesController(HousingDbContext db, RentAccountQueries rent, IStatementPdfWriter statementPdf, IPaymentsGateway payments,
            ArrearsPolicy arrearsPolicy, TenancyLetters letters, AuditWriter audit, IClock clock, ILogger<TenanciesController> logger)
        {
            _db = db;
            _rent = rent;
            _statementPdf = statementPdf;
            _payments = payments;
            _arrearsPolicy = arrearsPolicy;
            _letters = letters;
            _audit = audit;
            _clock = clock;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string q)
        {
            var query = _db.Tenancies.AsNoTracking().Include(t => t.Property).Include(t => t.Tenants).AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(t => t.Reference == q.ToUpper() || t.Tenants.Any(n => n.LastName.Contains(q)) || t.Property.AddressLine1.Contains(q));
            }
            else
            {
                var patch = User.Patch();
                if (patch != null) query = query.Where(t => t.Property.PatchCode == patch);
            }
            ViewBag.Q = q;
            return View(await query.Where(t => t.Status != TenancyStatus.Ended).OrderBy(t => t.Reference).Take(100).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var tenancy = await LoadAsync(id);
            if (tenancy == null) return NotFound();
            var today = _clock.Today;

            var arrearsCase = await _db.ArrearsCases.Include(c => c.Notes)
                .Where(c => c.TenancyId == id && c.ClosedOn == null).OrderByDescending(c => c.Id).FirstOrDefaultAsync();
            var arrangements = await _db.Arrangements.AsNoTracking().Where(a => a.TenancyId == id).OrderByDescending(a => a.StartDate).ToListAsync();
            var balance = await _rent.BalanceAsync(id);
            var recentTx = await _db.RentTransactions.AsNoTracking().Where(r => r.TenancyId == id && r.PostedOn >= today.AddDays(-56)).ToListAsync();

            var model = new TenancyDetailViewModel
            {
                Tenancy = tenancy,
                Balance = balance,
                RecentLines = (await _rent.StatementAsync(id, today.AddDays(-56), today)).Reverse().Take(20).ToList(),
                Mandates = await _db.Mandates.AsNoTracking().Where(m => m.TenancyId == id).OrderByDescending(m => m.CreatedAt).ToListAsync(),
                Arrangements = arrangements,
                ArrearsCase = arrearsCase,
                Decision = _arrearsPolicy.Evaluate(tenancy, balance, recentTx, arrearsCase, arrangements.FirstOrDefault(a => a.Status == ArrangementStatus.Active), today),
                Enquiries = await _db.Enquiries.AsNoTracking().Where(e => e.TenancyId == id).OrderByDescending(e => e.CreatedAt).ToListAsync(),
                Communications = await _db.Communications.AsNoTracking().Where(c => c.TenancyId == id).OrderByDescending(c => c.CreatedAt).Take(20).ToListAsync()
            };
            if (model.Arrangement.WeeklyAmount == 0 && balance > 0)
                model.Arrangement.WeeklyAmount = Math.Round(Math.Max(3.70m, balance / 40), 2);
            return View(model);
        }

        public async Task<IActionResult> Statement(int id, DateTime? from, DateTime? to)
        {
            var tenancy = await LoadAsync(id);
            if (tenancy == null) return NotFound();
            var model = new StatementViewModel
            {
                Tenancy = tenancy,
                From = (from ?? _clock.Today.AddMonths(-3)).Date,
                To = (to ?? _clock.Today).Date
            };
            model.Lines = await _rent.StatementAsync(id, model.From, model.To);
            return View(model);
        }

        public async Task<IActionResult> StatementPdf(int id, DateTime? from, DateTime? to)
        {
            var tenancy = await LoadAsync(id);
            if (tenancy == null) return NotFound();
            var f = (from ?? _clock.Today.AddMonths(-3)).Date;
            var t = (to ?? _clock.Today).Date;
            var lines = await _rent.StatementAsync(id, f, t);
            var pdf = _statementPdf.Write(new StatementHeader
            {
                TenancyReference = tenancy.Reference,
                TenantName = string.Join(" & ", tenancy.Tenants.Select(n => n.FullName)),
                Address = tenancy.Property.SingleLineAddress,
                From = f,
                To = t,
                WeeklyCharge = tenancy.WeeklyCharge
            }, lines);
            await _audit.WriteAsync(User.Login(), "Tenancy", tenancy.Reference, "StatementPrinted");
            return File(pdf, "application/pdf", $"Statement_{tenancy.Reference}_{t:yyyyMMdd}.pdf");
        }

        [HttpPost, Authorize(Policies.Income)]
        public async Task<IActionResult> PostAdjustment(int id, [Bind(Prefix = "Adjustment")] AdjustmentInput input)
        {
            var tenancy = await _db.Tenancies.FindAsync(id);
            if (tenancy == null) return NotFound();
            if (!ManualTypes.Contains(input.Type)) ModelState.AddModelError("Adjustment.Type", "This transaction type cannot be posted by hand.");
            if (input.Amount == 0) ModelState.AddModelError("Adjustment.Amount", "Enter an amount.");
            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(Details), new { id });
            }

            // Write-offs, refunds and cash are always credits/debits in a fixed direction; adjustments keep the sign entered.
            var amount = input.Type == TransactionType.WriteOff || input.Type == TransactionType.CashPayment ? -Math.Abs(input.Amount)
                : input.Type == TransactionType.Refund || input.Type == TransactionType.RechargeRepair ? Math.Abs(input.Amount)
                : input.Amount;
            if (input.Type == TransactionType.WriteOff && Math.Abs(amount) > 500 && !User.IsInRole(Hosting.Security.HousingRoles.Admin))
            {
                TempData["Error"] = "Write-offs over £500 need a manager (admin role).";
                return RedirectToAction(nameof(Details), new { id });
            }

            var now = _clock.Now;
            var tx = new RentTransaction
            {
                TenancyId = id,
                PostedOn = now,
                RentWeek = RentCalendar.WeekStart(now),
                Type = input.Type,
                Amount = Money.Round(amount),
                Reference = "MAN" + now.ToString("yyMMddHHmm"),
                Description = input.Description,
                Source = "BackOffice:" + User.Login()
            };
            _db.RentTransactions.Add(tx);
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(User.Login(), "Tenancy", tenancy.Reference, "Posted" + input.Type, new { tx.Amount, tx.Description });
            TempData["Message"] = $"{input.Type} of {Money.Format(tx.Amount)} posted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, Authorize(Policies.Income)]
        public async Task<IActionResult> CancelMandate(int id, int mandateId, string reason)
        {
            var mandate = await _db.Mandates.SingleOrDefaultAsync(m => m.Id == mandateId && m.TenancyId == id);
            if (mandate == null) return NotFound();
            if (mandate.Status == MandateStatus.Cancelled) return RedirectToAction(nameof(Details), new { id });

            try
            {
                if (!string.IsNullOrEmpty(mandate.ProviderMandateId))
                    await _payments.CancelMandateAsync(mandate.ProviderMandateId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cancelling mandate {MandateId} at Paystream failed", mandate.ProviderMandateId);
                TempData["Error"] = "Paystream did not accept the cancellation: " + ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }

            mandate.Status = MandateStatus.Cancelled;
            mandate.CancelledAt = _clock.Now;
            mandate.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled by staff" : reason;
            mandate.NextCollectionDate = null;
            mandate.ModifiedAt = _clock.Now;
            mandate.ModifiedBy = User.Login();
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(User.Login(), "DirectDebitMandate", mandate.ProviderMandateId, "Cancelled", new { reason });
            TempData["Message"] = "Direct debit cancelled.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, Authorize(Policies.Income)]
        public async Task<IActionResult> CreateArrangement(int id, [Bind(Prefix = "Arrangement")] ArrangementInput input)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Enter a weekly amount between £1 and £500.";
                return RedirectToAction(nameof(Details), new { id });
            }
            foreach (var existing in _db.Arrangements.Where(a => a.TenancyId == id && a.Status == ArrangementStatus.Active))
            {
                existing.Status = ArrangementStatus.Cancelled;
                existing.EndDate = _clock.Today;
            }
            var arrangement = new PaymentArrangement
            {
                TenancyId = id,
                WeeklyAmountTowardsArrears = input.WeeklyAmount,
                StartDate = input.StartDate.Date,
                Status = ArrangementStatus.Active,
                Notes = input.Notes,
                CreatedAt = _clock.Now,
                CreatedBy = User.Login()
            };
            _db.Arrangements.Add(arrangement);
            var arrearsCase = await OpenCaseAsync(id);
            if (arrearsCase != null)
                arrearsCase.Notes.Add(Note($"Payment arrangement agreed: {Money.Format(input.WeeklyAmount)} a week on top of the rent from {input.StartDate:dd/MM/yyyy}."));
            await _db.SaveChangesAsync();
            TempData["Message"] = "Payment arrangement recorded.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, Authorize(Policies.Income)]
        public async Task<IActionResult> EndArrangement(int id, int arrangementId, ArrangementStatus status)
        {
            var arrangement = await _db.Arrangements.SingleOrDefaultAsync(a => a.Id == arrangementId && a.TenancyId == id);
            if (arrangement == null) return NotFound();
            arrangement.Status = status == ArrangementStatus.Active ? ArrangementStatus.Cancelled : status;
            arrangement.EndDate = _clock.Today;
            arrangement.ModifiedAt = _clock.Now;
            arrangement.ModifiedBy = User.Login();
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, Authorize(Policies.Income)]
        public async Task<IActionResult> AddNote(int id, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return RedirectToAction(nameof(Details), new { id });
            var arrearsCase = await OpenCaseAsync(id, create: true);
            arrearsCase.Notes.Add(Note(text.Trim()));
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, Authorize(Policies.Income)]
        public async Task<IActionResult> Hold(int id, string reason)
        {
            var arrearsCase = await OpenCaseAsync(id, create: true);
            arrearsCase.OnHold = true;
            arrearsCase.HoldReason = string.IsNullOrWhiteSpace(reason) ? "Officer decision" : reason.Trim();
            arrearsCase.Notes.Add(Note("Case put on hold: " + arrearsCase.HoldReason));
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, Authorize(Policies.Income)]
        public async Task<IActionResult> Release(int id)
        {
            var arrearsCase = await OpenCaseAsync(id);
            if (arrearsCase != null)
            {
                arrearsCase.OnHold = false;
                arrearsCase.HoldReason = null;
                arrearsCase.Notes.Add(Note("Hold released"));
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// Serves a Notice of Seeking Possession (ground 8/10/11). Only the housing officer may do this,
        /// and only once the case has reached stage 3 or above.
        /// </summary>
        [HttpPost, Authorize(Policies.Income)]
        public async Task<IActionResult> ServeNotice(int id)
        {
            var tenancy = await LoadAsync(id, tracking: true);
            if (tenancy == null) return NotFound();
            var arrearsCase = await OpenCaseAsync(id);
            if (arrearsCase == null || arrearsCase.Stage < ArrearsStage.SecondLetter)
            {
                TempData["Error"] = "A notice can only be served once the second arrears letter has been sent.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (arrearsCase.OnHold)
            {
                TempData["Error"] = "The case is on hold: " + arrearsCase.HoldReason;
                return RedirectToAction(nameof(Details), new { id });
            }

            var balance = await _rent.BalanceAsync(id);
            var today = _clock.Today;
            var earliestProceedings = today.AddDays(28);
            DocumentRecord record;
            try
            {
                record = await _letters.CreateAsync(tenancy, "NOSP", "Notice of Seeking Possession", DocumentCategory.Notice,
                    new Dictionary<string, string>
                    {
                        ["Balance"] = Money.Format(balance),
                        ["WeeklyCharge"] = Money.Format(tenancy.WeeklyCharge),
                        ["WeeksInArrears"] = Math.Round(balance / Math.Max(1, tenancy.WeeklyCharge), 1).ToString("0.0"),
                        ["EarliestProceedingsDate"] = earliestProceedings.ToString("d MMMM yyyy"),
                        ["OfficerName"] = User.DisplayName(),
                        ["Grounds"] = tenancy.Type == TenancyType.Starter ? "Section 21 / Ground 8" : "Grounds 8, 10 and 11"
                    }, User.Login());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Generating the notice for {Tenancy} failed", tenancy.Reference);
                TempData["Error"] = "The notice could not be generated: " + ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }

            arrearsCase.Stage = ArrearsStage.NoticeOfSeekingPossession;
            arrearsCase.StageReachedOn = today;
            arrearsCase.BalanceAtStage = balance;
            arrearsCase.NextReviewOn = earliestProceedings;
            arrearsCase.ModifiedAt = _clock.Now;
            arrearsCase.ModifiedBy = User.Login();
            arrearsCase.Notes.Add(Note($"Notice of Seeking Possession served, balance {Money.Format(balance)}. Proceedings not before {earliestProceedings:dd/MM/yyyy}."));
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(User.Login(), "Tenancy", tenancy.Reference, "NoticeServed", new { balance, document = record?.DocumentKey });
            TempData["Message"] = "Notice generated and filed. Print it from the documents list and serve it by hand.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> RespondEnquiry(int id, int enquiryId, string response)
        {
            var enquiry = await _db.Enquiries.SingleOrDefaultAsync(e => e.Id == enquiryId && e.TenancyId == id);
            if (enquiry == null) return NotFound();
            if (string.IsNullOrWhiteSpace(response))
            {
                TempData["Error"] = "Enter a response.";
                return RedirectToAction(nameof(Details), new { id });
            }
            enquiry.Response = response.Trim();
            enquiry.RespondedAt = _clock.Now;
            enquiry.RespondedBy = User.Login();
            await _db.SaveChangesAsync();
            TempData["Message"] = "Response saved. The tenant will see it in the portal.";
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<Tenancy> LoadAsync(int id, bool tracking = false)
        {
            var query = _db.Tenancies.Include(t => t.Property).ThenInclude(p => p.Estate).Include(t => t.Tenants).AsQueryable();
            if (!tracking) query = query.AsNoTracking();
            return await query.SingleOrDefaultAsync(t => t.Id == id);
        }

        private async Task<ArrearsCase> OpenCaseAsync(int tenancyId, bool create = false)
        {
            var arrearsCase = await _db.ArrearsCases.Include(c => c.Notes)
                .Where(c => c.TenancyId == tenancyId && c.ClosedOn == null).OrderByDescending(c => c.Id).FirstOrDefaultAsync();
            if (arrearsCase == null && create)
            {
                arrearsCase = new ArrearsCase
                {
                    TenancyId = tenancyId,
                    Stage = ArrearsStage.None,
                    StageReachedOn = _clock.Today,
                    BalanceAtStage = await _rent.BalanceAsync(tenancyId),
                    CreatedAt = _clock.Now,
                    CreatedBy = User.Login()
                };
                _db.ArrearsCases.Add(arrearsCase);
            }
            return arrearsCase;
        }

        private ArrearsCaseNote Note(string text) => new ArrearsCaseNote { CreatedAt = _clock.Now, CreatedBy = User.Login(), Text = text };
    }
}
