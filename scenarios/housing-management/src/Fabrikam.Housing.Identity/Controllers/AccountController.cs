using System;
using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Identity.Data;
using Fabrikam.Housing.Identity.Models;
using Fabrikam.Housing.Identity.Services;
using Fabrikam.Housing.Integration.Messaging;
using Fabrikam.Housing.Tenancies;
using IdentityServer4.Events;
using IdentityServer4.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fabrikam.Housing.Identity.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _users;
        private readonly SignInManager<ApplicationUser> _signIn;
        private readonly StaffSignIn _staff;
        private readonly IIdentityServerInteractionService _interaction;
        private readonly IEventService _events;
        private readonly HousingDbContext _housing;
        private readonly AuditWriter _audit;
        private readonly IEmailSender _email;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AccountController> _logger;

        public AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, StaffSignIn staff,
            IIdentityServerInteractionService interaction, IEventService events, HousingDbContext housing, AuditWriter audit,
            IEmailSender email, IConfiguration configuration, ILogger<AccountController> logger)
        {
            _users = users;
            _signIn = signIn;
            _staff = staff;
            _interaction = interaction;
            _events = events;
            _housing = housing;
            _audit = audit;
            _email = email;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login(string returnUrl)
        {
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var context = await _interaction.GetAuthorizationContextAsync(model.ReturnUrl);

            if (_staff.IsStaffName(model.UserName))
            {
                ApplicationUser staff;
                try
                {
                    staff = await _staff.AuthenticateAsync(model.UserName, model.Password);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Directory sign-in failed for {User}", model.UserName);
                    ModelState.AddModelError("", "We could not reach the staff directory. Please try again or call the IT service desk on extension 4400.");
                    return View(model);
                }
                if (staff == null)
                {
                    await _events.RaiseAsync(new UserLoginFailureEvent(model.UserName, "invalid directory credentials", clientId: context?.Client.ClientId));
                    ModelState.AddModelError("", "Your username or password is not correct, or your account does not have access to Fabrikam Housing.");
                    return View(model);
                }
                await _signIn.SignInAsync(staff, isPersistent: false, authenticationMethod: "ldap");
                await _events.RaiseAsync(new UserLoginSuccessEvent(staff.UserName, staff.Id, staff.FullName, clientId: context?.Client.ClientId));
                return AfterSignIn(model.ReturnUrl, context != null);
            }

            var user = await _users.FindByEmailAsync(model.UserName) ?? await _users.FindByNameAsync(model.UserName);
            if (user == null || user.IsStaff)
            {
                ModelState.AddModelError("", "Your e-mail address or password is not correct.");
                return View(model);
            }

            var result = await _signIn.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                user.LastSignInAt = DateTime.Now;
                await _users.UpdateAsync(user);
                await _events.RaiseAsync(new UserLoginSuccessEvent(user.UserName, user.Id, user.FullName, clientId: context?.Client.ClientId));
                return AfterSignIn(model.ReturnUrl, context != null);
            }

            await _events.RaiseAsync(new UserLoginFailureEvent(model.UserName, result.IsLockedOut ? "locked out" : "invalid credentials", clientId: context?.Client.ClientId));
            ModelState.AddModelError("", result.IsLockedOut
                ? "Your account is locked for 15 minutes after too many attempts. You can reset your password if you have forgotten it."
                : "Your e-mail address or password is not correct.");
            return View(model);
        }

        private IActionResult AfterSignIn(string returnUrl, bool fromClient)
        {
            if (fromClient || _interaction.IsValidReturnUrl(returnUrl)) return Redirect(returnUrl);
            if (Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
            return Redirect("~/");
        }

        [HttpGet]
        public async Task<IActionResult> Logout(string logoutId)
        {
            // Signed-out users and requests from a client (with a logout id) are signed out without a prompt.
            if (User?.Identity.IsAuthenticated != true) return await LoggedOut(logoutId);
            var context = await _interaction.GetLogoutContextAsync(logoutId);
            if (context?.ShowSignoutPrompt == false) return await LoggedOut(logoutId);
            ViewBag.LogoutId = logoutId;
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> Logout(string logoutId, bool confirm) => LoggedOut(logoutId);

        private async Task<IActionResult> LoggedOut(string logoutId)
        {
            var context = await _interaction.GetLogoutContextAsync(logoutId);
            if (User?.Identity.IsAuthenticated == true)
            {
                var name = User.Identity.Name;
                await _signIn.SignOutAsync();
                await _events.RaiseAsync(new UserLogoutSuccessEvent(User.FindFirst("sub")?.Value, name));
            }
            if (!string.IsNullOrEmpty(context?.PostLogoutRedirectUri)) return Redirect(context.PostLogoutRedirectUri);
            return View("LoggedOut", new LoggedOutViewModel
            {
                PostLogoutRedirectUri = context?.PostLogoutRedirectUri,
                ClientName = context?.ClientName ?? context?.ClientId,
                SignOutIframeUrl = context?.SignOutIFrameUrl
            });
        }

        [HttpGet]
        public IActionResult Register(string returnUrl) => View(new RegisterViewModel { ReturnUrl = returnUrl });

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var reference = model.TenancyReference.Trim().ToUpperInvariant();
            var tenancy = await _housing.Tenancies
                .Include(t => t.Property)
                .Include(t => t.Tenants)
                .FirstOrDefaultAsync(t => t.Reference == reference);
            var tenant = tenancy?.Tenants.FirstOrDefault(t => TenancyVerification.Matches(t, reference, model.DateOfBirth.Value, model.Postcode));
            if (tenant == null)
            {
                _logger.LogInformation("Registration details did not match for tenancy {Reference}", reference);
                ModelState.AddModelError("", "We could not match these details to a tenancy. Check them against your tenancy agreement or rent statement, or call us on 0300 123 4567.");
                return View(model);
            }
            if (!string.IsNullOrEmpty(tenant.PortalSubjectId))
            {
                ModelState.AddModelError("", "There is already an account for you. Sign in, or reset your password if you have forgotten it.");
                return View(model);
            }
            if (await _users.FindByEmailAsync(model.Email) != null)
            {
                ModelState.AddModelError(nameof(model.Email), "This e-mail address is already used by another account.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email.Trim(),
                Email = model.Email.Trim(),
                FullName = tenant.FirstName + " " + tenant.LastName,
                TenantId = tenant.Id,
                TenancyReference = tenancy.Reference,
                CreatedAt = DateTime.Now
            };
            var result = await _users.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors) ModelState.AddModelError(nameof(model.Password), error.Description);
                return View(model);
            }

            tenant.PortalSubjectId = user.Id;
            if (string.IsNullOrEmpty(tenant.Email)) tenant.Email = user.Email;
            tenant.ModifiedAt = DateTime.Now;
            tenant.ModifiedBy = "portal-registration";
            await _housing.SaveChangesAsync();
            await _audit.WriteAsync(user.Email, "Tenant", tenant.Id.ToString(), "PortalRegistered", new { tenancy.Reference });

            await _signIn.SignInAsync(user, isPersistent: false);
            if (_interaction.IsValidReturnUrl(model.ReturnUrl)) return Redirect(model.ReturnUrl);
            return Redirect(_configuration["Clients:TenantPortal:BaseUrl"] ?? "~/");
        }

        [HttpGet]
        public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = await _users.FindByEmailAsync(model.Email);
            if (user != null && !user.IsStaff)
            {
                var token = await _users.GeneratePasswordResetTokenAsync(user);
                var link = Url.Action(nameof(ResetPassword), "Account", new { userId = user.Id, token }, Request.Scheme);
                try
                {
                    await _email.SendAsync(user.Email, "Reset your Fabrikam Homes password",
                        $"<p>Dear {System.Net.WebUtility.HtmlEncode(user.FullName)},</p>" +
                        $"<p>Use this link to choose a new password for the tenant portal. The link works for 24 hours.</p>" +
                        $"<p><a href=\"{link}\">Reset my password</a></p>" +
                        "<p>If you did not ask for this, you can ignore this e-mail.</p><p>Fabrikam Homes</p>");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Password reset e-mail to {Email} failed", user.Email);
                }
            }
            return View("ForgotPasswordConfirmation");
        }

        [HttpGet]
        public IActionResult ResetPassword(string userId, string token) => View(new ResetPasswordViewModel { UserId = userId, Token = token });

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = await _users.FindByIdAsync(model.UserId);
            if (user == null || user.IsStaff)
            {
                ModelState.AddModelError("", "This reset link is not valid.");
                return View(model);
            }
            var result = await _users.ResetPasswordAsync(user, model.Token, model.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
                return View(model);
            }
            await _users.SetLockoutEndDateAsync(user, null);
            TempData["Message"] = "Your password has been changed. You can now sign in.";
            return RedirectToAction(nameof(Login));
        }
    }
}
