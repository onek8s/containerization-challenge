using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using log4net;
using VanArsdel.Integration.Tracking.Models;
using VanArsdel.Integration.Tracking.Security;

namespace VanArsdel.Integration.Tracking.Controllers
{
    public class AccountController : Controller
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AccountController));
        private readonly SignInService _signIn;
        private readonly IPortalUserStore _users;
        private readonly PasswordHasher _hasher;

        public AccountController(SignInService signIn, IPortalUserStore users, PasswordHasher hasher)
        {
            _signIn = signIn;
            _users = users;
            _hasher = hasher;
        }

        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            return View(new LoginModel { ReturnUrl = returnUrl });
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            PortalUser user;
            var result = _signIn.Validate(model.UserName, model.Password, Request.UserHostAddress, out user);
            switch (result)
            {
                case SignInResult.LockedOut:
                    ModelState.AddModelError(string.Empty, "The account is locked. Try again in " + _signIn.LockoutMinutes + " minutes or contact customer service.");
                    return View(model);
                case SignInResult.Failed:
                    ModelState.AddModelError(string.Empty, "The user name or password is incorrect.");
                    return View(model);
            }

            Response.Cookies.Add(_signIn.CreateCookie(user, model.RememberMe));
            Log.InfoFormat("{0} signed in from {1}", user.UserName, Request.UserHostAddress);
            if (result == SignInResult.MustChangePassword)
            {
                TempData["Message"] = "Your password has expired. Choose a new one.";
                return RedirectToAction("ChangePassword");
            }

            return Url.IsLocalUrl(model.ReturnUrl) ? (ActionResult)Redirect(model.ReturnUrl) : RedirectToAction("Index", "Orders");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Abandon();
            return RedirectToAction("Login");
        }

        public ActionResult ChangePassword()
        {
            return View(new ChangePasswordModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(ChangePasswordModel model)
        {
            var user = _users.Find(User.Identity.Name);
            bool ignored;
            if (user == null || !_hasher.Verify(model.CurrentPassword, user.PasswordHash, out ignored))
            {
                ModelState.AddModelError("CurrentPassword", "The current password is incorrect.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            _signIn.ChangePassword(user, model.NewPassword);
            Log.InfoFormat("{0} changed their password", user.UserName);
            TempData["Message"] = "Password changed.";
            return RedirectToAction("Index", "Orders");
        }

        [AllowAnonymous]
        public ActionResult Denied()
        {
            Response.StatusCode = 403;
            Response.TrySkipIisCustomErrors = true;
            return View();
        }
    }
}
