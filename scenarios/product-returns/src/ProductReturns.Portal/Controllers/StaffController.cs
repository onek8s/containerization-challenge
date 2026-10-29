using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ProductReturns.Portal.Models;
using ProductReturns.Portal.Services;

namespace ProductReturns.Portal.Controllers
{
    [Authorize(Policy = StaffRoles.AnyStaff)]
    [Route("staff")]
    public class StaffController : Controller
    {
        private const string FilterKey = "StaffQueueFilter";

        private readonly ReturnsApiClient _api;
        private readonly IStaffDirectory _directory;
        private readonly ILogger<StaffController> _logger;

        public StaffController(ReturnsApiClient api, IStaffDirectory directory, ILogger<StaffController> logger)
        {
            _api = api;
            _directory = directory;
            _logger = logger;
        }

        private string Actor => User.Identity.Name;

        [AllowAnonymous]
        [HttpGet("login")]
        public IActionResult Login(string returnUrl)
        {
            return View(new StaffLoginModel { ReturnUrl = returnUrl });
        }

        [AllowAnonymous]
        [HttpPost("login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(StaffLoginModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            StaffUser user;
            try
            {
                user = _directory.SignIn(model.UserName, model.Password);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Directory sign-in failed for {User}", model.UserName);
                ModelState.AddModelError(string.Empty, "Sign-in is not available right now.");
                return View(model);
            }

            if (user == null || user.Roles.Count == 0)
            {
                ModelState.AddModelError(string.Empty, user == null ? "Wrong user name or password." : "You do not have access to returns.");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.AccountName),
                new Claim("display_name", user.DisplayName ?? user.AccountName)
            };
            claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Remove(FilterKey);
            return RedirectToAction(nameof(Login));
        }

        [AllowAnonymous]
        [HttpGet("denied")]
        public IActionResult Denied() => View();

        [HttpGet("")]
        public async Task<IActionResult> Index([FromQuery] StaffQueueFilter filter, bool reset, CancellationToken ct)
        {
            // The queue filter is remembered for the session so the agent returns to the same list.
            if (!reset && !Request.Query.Any())
            {
                filter = HttpContext.Session.GetObject<StaffQueueFilter>(FilterKey) ?? new StaffQueueFilter();
            }

            HttpContext.Session.SetObject(FilterKey, filter);
            var model = new StaffQueueModel { Filter = filter };
            var result = await _api.SearchAsync(filter.Status, filter.Query, Math.Max(1, filter.Page), ct);
            var counts = await _api.SummaryAsync(ct);
            if (!result.Ok)
            {
                model.Error = result.Error;
            }

            model.Result = result.Value;
            model.Counts = counts.Value ?? new Dictionary<string, int>();
            return View(model);
        }

        [HttpGet("returns/{id}")]
        public async Task<IActionResult> Detail(string id, CancellationToken ct)
        {
            var result = await _api.GetReturnAsync(id, null, ct);
            if (!result.Ok)
            {
                return NotFound();
            }

            return View(new StaffDetailModel
            {
                Return = result.Value,
                Error = TempData["Error"] as string,
                IsSupervisor = User.IsInRole(StaffRoles.Supervisor)
            });
        }

        [HttpPost("returns/{id}/approve")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(string id, CancellationToken ct) =>
            AfterAction(id, await _api.ApproveAsync(id, Actor, ct));

        [HttpPost("returns/{id}/reject")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(string id, string reason, CancellationToken ct) =>
            AfterAction(id, await _api.RejectAsync(id, reason, Actor, ct));

        [HttpPost("returns/{id}/notes")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNote(string id, string text, CancellationToken ct) =>
            AfterAction(id, await _api.AddNoteAsync(id, text, Actor, ct));

        [Authorize(Policy = StaffRoles.Supervisor)]
        [HttpPost("returns/{id}/complete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteInspection(string id, CancellationToken ct) =>
            AfterAction(id, await _api.CompleteInspectionAsync(id, Actor, ct));

        [HttpGet("returns/{id}/label")]
        public async Task<IActionResult> Label(string id, CancellationToken ct)
        {
            var stream = await _api.GetLabelAsync(id, null, ct);
            return stream == null ? (IActionResult)NotFound() : File(stream, "application/pdf", id + ".pdf");
        }

        private IActionResult AfterAction<T>(string id, ApiResult<T> result)
        {
            if (!result.Ok)
            {
                TempData["Error"] = result.Error;
            }

            return RedirectToAction(nameof(Detail), new { id });
        }
    }
}
