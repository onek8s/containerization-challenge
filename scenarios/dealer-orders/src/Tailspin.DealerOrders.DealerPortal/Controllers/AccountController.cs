using System;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using log4net;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Tailspin.DealerOrders.Data;
using Tailspin.DealerOrders.DealerPortal.Infrastructure;
using Tailspin.DealerOrders.DealerPortal.Models;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.DealerPortal.Controllers
{
    public class AccountController : PortalController
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AccountController));

        private readonly PasswordMail _passwordMail;
        private readonly IDealerStore _dealers;
        private readonly IClock _clock;

        public AccountController(PasswordMail passwordMail, IDealerStore dealers, IClock clock)
        {
            _passwordMail = passwordMail;
            _dealers = dealers;
            _clock = clock;
        }

        private PortalUserManager Users => HttpContext.GetOwinContext().GetUserManager<PortalUserManager>();
        private PortalSignInManager SignIn => HttpContext.GetOwinContext().Get<PortalSignInManager>();

        [AllowAnonymous]
        public ActionResult Login(string returnUrl) => View(new LoginModel { ReturnUrl = returnUrl });

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await Users.FindByNameAsync(model.UserName);
            if (user != null && user.Disabled)
            {
                ModelState.AddModelError("", "Your account has been disabled. Contact your dealer administrator.");
                return View(model);
            }
            if (user != null)
            {
                var dealer = _dealers.Get(user.DealerId);
                if (dealer == null || dealer.Status == DealerStatus.Terminated)
                {
                    ModelState.AddModelError("", "Your dealership no longer has portal access.");
                    return View(model);
                }
            }

            var result = await SignIn.PasswordSignInAsync(model.UserName, model.Password, model.RememberMe, shouldLockout: true);
            switch (result)
            {
                case SignInStatus.Success:
                    user.LastLoginUtc = _clock.UtcNow;
                    await Users.UpdateAsync(user);
                    Log.InfoFormat("{0} signed in", model.UserName);
                    return Url.IsLocalUrl(model.ReturnUrl) ? (ActionResult)Redirect(model.ReturnUrl) : RedirectToAction("Index", "Home");
                case SignInStatus.LockedOut:
                    Log.WarnFormat("{0} locked out after failed sign-ins", model.UserName);
                    return View("Locked");
                default:
                    ModelState.AddModelError("", "The user name or password is not correct.");
                    return View(model);
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            HttpContext.GetOwinContext().Authentication.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            return RedirectToAction("Login");
        }

        [AllowAnonymous]
        public ActionResult ForgotPassword() => View(new ForgotPasswordModel());

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<ActionResult> ForgotPassword(ForgotPasswordModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = await Users.FindByEmailAsync(model.Email);
            if (user != null && !user.Disabled)
            {
                var code = await Users.GeneratePasswordResetTokenAsync(user.Id);
                var link = Url.Action("ResetPassword", "Account", new { userId = user.Id, code }, Request.Url.Scheme);
                _passwordMail.Send(user, link);
            }
            // Same answer whether or not the address is known.
            return View("ForgotPasswordSent");
        }

        [AllowAnonymous]
        public ActionResult ResetPassword(string userId, string code) =>
            string.IsNullOrEmpty(code) ? (ActionResult)HttpNotFound() : View(new ResetPasswordModel { UserId = userId, Code = code });

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetPassword(ResetPasswordModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var result = await Users.ResetPasswordAsync(model.UserId, model.Code, model.Password);
            if (result.Succeeded)
            {
                await Users.ResetAccessFailedCountAsync(model.UserId);
                await Users.SetLockoutEndDateAsync(model.UserId, DateTimeOffset.UtcNow);
                Flash("Your password has been changed. Sign in with the new password.");
                return RedirectToAction("Login");
            }
            foreach (var error in result.Errors) ModelState.AddModelError("", error);
            return View(model);
        }

        public ActionResult ChangePassword() => View(new ChangePasswordModel());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangePassword(ChangePasswordModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var userId = User.Identity.GetUserId();
            var result = await Users.ChangePasswordAsync(userId, model.OldPassword, model.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors) ModelState.AddModelError("", error);
                return View(model);
            }
            await SignIn.SignInAsync(await Users.FindByIdAsync(userId), false, false);
            Flash("Your password has been changed.");
            return RedirectToAction("Index", "Home");
        }
    }
}
