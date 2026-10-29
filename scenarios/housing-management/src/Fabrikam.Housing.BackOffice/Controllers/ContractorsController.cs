using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Fabrikam.Housing.BackOffice.Infrastructure;
using Fabrikam.Housing.BackOffice.Models;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Contractors;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Repairs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fabrikam.Housing.BackOffice.Controllers
{
    public class ContractorsController : Controller
    {
        private readonly HousingDbContext _db;
        private readonly AuditWriter _audit;
        private readonly IClock _clock;

        public ContractorsController(HousingDbContext db, AuditWriter audit, IClock clock)
        {
            _db = db;
            _audit = audit;
            _clock = clock;
        }

        public async Task<IActionResult> Index()
        {
            var open = await OpenJobsAsync();
            ViewBag.OpenJobs = open;
            return View(await _db.Contractors.AsNoTracking().OrderBy(c => c.Code).ToListAsync());
        }

        [Authorize(Policies.Admin)]
        public async Task<IActionResult> Edit(int? id)
        {
            var contractor = id.HasValue ? await _db.Contractors.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id) : new Contractor { Active = true, WeeklyCapacity = 20, Patches = "*" };
            if (contractor == null) return NotFound();
            return View(await EditModelAsync(contractor));
        }

        [HttpPost, Authorize(Policies.Admin)]
        public async Task<IActionResult> Edit(int? id, Contractor input, List<string> selectedTrades)
        {
            var contractor = id.HasValue ? await _db.Contractors.FindAsync(id.Value) : new Contractor();
            if (contractor == null) return NotFound();
            if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Name))
                ModelState.AddModelError("", "Code and name are required.");
            if (selectedTrades == null || selectedTrades.Count == 0)
                ModelState.AddModelError("", "Choose at least one trade.");
            if (!string.IsNullOrWhiteSpace(input.BankSortCode) && input.BankSortCode.Count(char.IsDigit) != 6)
                ModelState.AddModelError("", "Sort code must have six digits.");
            if (!string.IsNullOrWhiteSpace(input.BankAccountNumber) && (input.BankAccountNumber.Length != 8 || !input.BankAccountNumber.All(char.IsDigit)))
                ModelState.AddModelError("", "Account number must have eight digits.");
            if (!ModelState.IsValid)
            {
                var model = await EditModelAsync(input);
                model.SelectedTrades = selectedTrades ?? new List<string>();
                return View(model);
            }

            var bankChanged = contractor.BankSortCode != input.BankSortCode || contractor.BankAccountNumber != input.BankAccountNumber;
            contractor.Code = input.Code.Trim().ToUpperInvariant();
            contractor.Name = input.Name.Trim();
            contractor.Trades = string.Join(",", selectedTrades);
            contractor.Patches = string.IsNullOrWhiteSpace(input.Patches) ? "*" : input.Patches.Replace(" ", "").ToUpperInvariant();
            contractor.OutOfHours = input.OutOfHours;
            contractor.GasSafeRegistered = input.GasSafeRegistered;
            contractor.GasSafeNumber = input.GasSafeNumber;
            contractor.WeeklyCapacity = input.WeeklyCapacity;
            contractor.ContactEmail = input.ContactEmail;
            contractor.ContactPhone = input.ContactPhone;
            contractor.RemittanceEmail = input.RemittanceEmail;
            contractor.VatRegistered = input.VatRegistered;
            contractor.VatNumber = input.VatNumber;
            contractor.BankSortCode = input.BankSortCode;
            contractor.BankAccountNumber = input.BankAccountNumber;
            contractor.BankAccountName = input.BankAccountName;
            contractor.Active = input.Active;
            if (contractor.Id == 0)
            {
                contractor.ApiClientId = "contractor-" + contractor.Code.ToLowerInvariant();
                contractor.CreatedAt = _clock.Now;
                contractor.CreatedBy = User.Login();
                _db.Contractors.Add(contractor);
            }
            else
            {
                contractor.ModifiedAt = _clock.Now;
                contractor.ModifiedBy = User.Login();
            }
            await _db.SaveChangesAsync();
            // Bank detail changes are a fraud risk: they are audited separately and checked by finance before the next payment run.
            await _audit.WriteAsync(User.Login(), "Contractor", contractor.Code, bankChanged ? "BankDetailsChanged" : "Updated",
                bankChanged ? new { contractor.BankSortCode, AccountEnding = contractor.BankAccountNumber?.Substring(Math.Max(0, contractor.BankAccountNumber.Length - 4)) } : null);
            TempData["Message"] = "Contractor saved.";
            return RedirectToAction(nameof(Edit), new { id = contractor.Id });
        }

        /// <summary>
        /// Issues a new client secret for the contractor API. The secret is shown once; only its SHA-256 is stored.
        /// </summary>
        [HttpPost, Authorize(Policies.Admin)]
        public async Task<IActionResult> NewSecret(int id)
        {
            var contractor = await _db.Contractors.FindAsync(id);
            if (contractor == null) return NotFound();
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            var secret = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            using (var sha = SHA256.Create())
                contractor.ApiClientSecretHash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(secret)));
            contractor.ApiSecretIssuedAt = _clock.Now;
            if (string.IsNullOrEmpty(contractor.ApiClientId)) contractor.ApiClientId = "contractor-" + contractor.Code.ToLowerInvariant();
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(User.Login(), "Contractor", contractor.Code, "ApiSecretIssued");

            var model = await EditModelAsync(contractor);
            model.NewSecret = secret;
            return View(nameof(Edit), model);
        }

        [Authorize(Policies.Finance)]
        public async Task<IActionResult> Invoices(InvoiceStatus? status)
        {
            var invoices = await _db.ContractorInvoices.AsNoTracking()
                .Include(i => i.Contractor)
                .Include(i => i.WorkOrder).ThenInclude(w => w.Lines)
                .Include(i => i.WorkOrder).ThenInclude(w => w.Property)
                .Where(i => i.Status == (status ?? InvoiceStatus.Received))
                .OrderBy(i => i.InvoiceDate).Take(200).ToListAsync();
            ViewBag.Status = status ?? InvoiceStatus.Received;
            return View(invoices.Select(i => new InvoiceReviewItem
            {
                Invoice = i,
                PricedWork = i.WorkOrder?.Lines.Sum(l => l.LineTotal) ?? 0,
                MatchProblem = InvoiceMatching.Check(i, i.WorkOrder, i.Contractor)
            }).ToList());
        }

        [HttpPost, Authorize(Policies.Finance)]
        public async Task<IActionResult> ReviewInvoice(int id, string decision, string reason)
        {
            var invoice = await _db.ContractorInvoices.Include(i => i.Contractor).Include(i => i.WorkOrder).ThenInclude(w => w.Lines)
                .SingleOrDefaultAsync(i => i.Id == id);
            if (invoice == null) return NotFound();
            if (invoice.Status != InvoiceStatus.Received && invoice.Status != InvoiceStatus.Queried)
            {
                TempData["Error"] = $"Invoice {invoice.InvoiceNumber} is already {invoice.Status}.";
                return RedirectToAction(nameof(Invoices));
            }

            switch (decision)
            {
                case "approve":
                    var problem = InvoiceMatching.Check(invoice, invoice.WorkOrder, invoice.Contractor);
                    if (problem != null && string.IsNullOrWhiteSpace(reason))
                    {
                        TempData["Error"] = $"{invoice.InvoiceNumber}: {problem}. Enter a reason to approve anyway.";
                        return RedirectToAction(nameof(Invoices));
                    }
                    invoice.Status = InvoiceStatus.Approved;
                    invoice.ApprovedBy = User.Login();
                    invoice.ApprovedAt = _clock.Now;
                    invoice.QueryReason = problem == null ? null : "Approved over tolerance: " + reason;
                    break;
                case "query":
                    invoice.Status = InvoiceStatus.Queried;
                    invoice.QueryReason = reason;
                    break;
                case "reject":
                    invoice.Status = InvoiceStatus.Rejected;
                    invoice.QueryReason = reason;
                    break;
                default:
                    return BadRequest();
            }
            invoice.ModifiedAt = _clock.Now;
            invoice.ModifiedBy = User.Login();
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(User.Login(), "ContractorInvoice", invoice.Contractor.Code + "/" + invoice.InvoiceNumber, "Invoice" + invoice.Status, new { reason });
            TempData["Message"] = $"Invoice {invoice.InvoiceNumber} {invoice.Status.ToString().ToLowerInvariant()}.";
            return RedirectToAction(nameof(Invoices));
        }

        private async Task<ContractorEditViewModel> EditModelAsync(Contractor contractor)
        {
            var open = await OpenJobsAsync();
            return new ContractorEditViewModel
            {
                Contractor = contractor,
                AllTrades = Enum.GetNames(typeof(Trade)),
                SelectedTrades = (contractor.Trades ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList(),
                OpenJobs = open.TryGetValue(contractor.Id, out var n) ? n : 0
            };
        }

        private Task<Dictionary<int, int>> OpenJobsAsync() =>
            _db.WorkOrders.Where(w => w.ContractorId != null && w.Status != WorkOrderStatus.Completed && w.Status != WorkOrderStatus.Cancelled)
                .GroupBy(w => w.ContractorId.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);
    }
}
