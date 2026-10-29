using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProductReturns.Core;
using ProductReturns.Core.Api;
using ProductReturns.Portal.Models;
using ProductReturns.Portal.Services;
using Microsoft.Extensions.Configuration;

namespace ProductReturns.Portal.Controllers
{
    /// <summary>Customer return wizard: select items, review, submit. Draft is kept in session.</summary>
    public class ReturnController : Controller
    {
        private const string LastSubmittedKey = "LastSubmitted";

        private readonly ReturnsApiClient _api;
        private readonly IConfiguration _configuration;

        public ReturnController(ReturnsApiClient api, IConfiguration configuration)
        {
            _api = api;
            _configuration = configuration;
        }

        private ReturnDraft Draft => HttpContext.Session.GetObject<ReturnDraft>(HomeController.DraftKey);

        [HttpGet]
        public async Task<IActionResult> Select(CancellationToken ct)
        {
            var draft = Draft;
            if (draft == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var order = await _api.GetOrderAsync(draft.OrderNumber, draft.Email, ct);
            if (!order.Ok)
            {
                return RedirectToAction("Index", "Home");
            }

            var model = new SelectLinesModel { Order = order.Value };
            foreach (var line in order.Value.Lines)
            {
                var selected = draft.Lines.FirstOrDefault(l => l.LineNumber == line.LineNumber);
                model.Selections.Add(new LineSelection
                {
                    LineNumber = line.LineNumber,
                    Quantity = selected?.Quantity ?? 0,
                    ReasonCode = selected?.ReasonCode,
                    Comment = selected?.Comment
                });
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Select(List<LineSelection> selections, CancellationToken ct)
        {
            var draft = Draft;
            if (draft == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var order = await _api.GetOrderAsync(draft.OrderNumber, draft.Email, ct);
            if (!order.Ok)
            {
                return RedirectToAction("Index", "Home");
            }

            selections = selections ?? new List<LineSelection>();
            var chosen = selections.Where(s => s.Quantity > 0).ToList();
            if (chosen.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Select at least one item to return.");
            }

            foreach (var s in chosen)
            {
                var line = order.Value.Lines.FirstOrDefault(l => l.LineNumber == s.LineNumber);
                if (line == null || s.Quantity > line.ReturnableQuantity)
                {
                    ModelState.AddModelError(string.Empty, "The quantity for " + (line?.Description ?? "an item") + " is not available.");
                }
                else if (ReturnReasons.Find(s.ReasonCode) == null)
                {
                    ModelState.AddModelError(string.Empty, "Choose a reason for " + line.Description + ".");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(new SelectLinesModel { Order = order.Value, Selections = selections });
            }

            draft.Lines = chosen.Select(s => new CreateReturnLine
            {
                LineNumber = s.LineNumber,
                Quantity = s.Quantity,
                ReasonCode = s.ReasonCode,
                Comment = s.Comment
            }).ToList();
            HttpContext.Session.SetObject(HomeController.DraftKey, draft);
            return RedirectToAction(nameof(Review));
        }

        [HttpGet]
        public async Task<IActionResult> Review(CancellationToken ct)
        {
            var draft = Draft;
            if (draft == null || draft.Lines.Count == 0)
            {
                return RedirectToAction("Index", "Home");
            }

            var order = await _api.GetOrderAsync(draft.OrderNumber, draft.Email, ct);
            if (!order.Ok)
            {
                return RedirectToAction("Index", "Home");
            }

            var value = draft.Lines.Sum(l => l.Quantity * (order.Value.Lines.FirstOrDefault(o => o.LineNumber == l.LineNumber)?.UnitPrice ?? 0m));
            var labelFee = draft.Lines.All(l => !ReturnReasons.IsCompanyFault(l.ReasonCode))
                ? _configuration.GetValue("ReturnPolicy:LabelFee", 5.95m)
                : 0m;
            return View(new ReviewModel { Draft = draft, Order = order.Value, ItemsValue = value, LabelFee = labelFee });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(CancellationToken ct)
        {
            var draft = Draft;
            if (draft == null || draft.Lines.Count == 0)
            {
                // Double submit: show the return that was just created.
                var last = HttpContext.Session.GetString(LastSubmittedKey);
                return last != null ? RedirectToAction(nameof(Submitted), new { id = last }) : RedirectToAction("Index", "Home");
            }

            var result = await _api.CreateReturnAsync(new CreateReturnRequest
            {
                OrderNumber = draft.OrderNumber,
                Email = draft.Email,
                Lines = draft.Lines
            }, "customer", ct);

            if (!result.Ok)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction(nameof(Select));
            }

            HttpContext.Session.Remove(HomeController.DraftKey);
            HttpContext.Session.SetString(LastSubmittedKey, result.Value.RmaNumber);
            HttpContext.Session.SetString("StatusEmail", draft.Email);
            return RedirectToAction(nameof(Submitted), new { id = result.Value.RmaNumber });
        }

        [HttpGet]
        public IActionResult Submitted(string id)
        {
            if (!string.Equals(HttpContext.Session.GetString(LastSubmittedKey), id, StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(Status));
            }

            ViewBag.RmaNumber = id;
            return View();
        }

        [HttpGet]
        public IActionResult Status()
        {
            return View(new StatusLookupModel { RmaNumber = HttpContext.Session.GetString(LastSubmittedKey), Email = HttpContext.Session.GetString("StatusEmail") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Status(StatusLookupModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _api.GetReturnAsync(model.RmaNumber.Trim(), model.Email.Trim(), ct);
            if (!result.Ok)
            {
                ModelState.AddModelError(string.Empty, result.StatusCode == 404 ? "We could not find this return." : result.Error);
                return View(model);
            }

            HttpContext.Session.SetString("StatusEmail", model.Email.Trim());
            return View("Detail", new CustomerReturnModel { Return = result.Value, Email = model.Email.Trim() });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(string rma, CancellationToken ct)
        {
            var email = HttpContext.Session.GetString("StatusEmail");
            if (email == null)
            {
                return RedirectToAction(nameof(Status));
            }

            var result = await _api.CancelReturnAsync(rma, email, ct);
            if (!result.Ok)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction(nameof(Status));
            }

            return View("Detail", new CustomerReturnModel { Return = result.Value, Email = email });
        }

        [HttpGet]
        public async Task<IActionResult> Label(string id, CancellationToken ct)
        {
            var email = HttpContext.Session.GetString("StatusEmail");
            if (email == null)
            {
                return RedirectToAction(nameof(Status));
            }

            var stream = await _api.GetLabelAsync(id, email, ct);
            if (stream == null)
            {
                return NotFound();
            }

            return File(stream, "application/pdf", id + ".pdf");
        }
    }
}
