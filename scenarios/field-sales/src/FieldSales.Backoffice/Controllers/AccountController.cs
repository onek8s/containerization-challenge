using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using log4net;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Wingtip.FieldSales.Backoffice.Models;
using Wingtip.FieldSales.Backoffice.Services;
using Wingtip.FieldSales.Data;

namespace Wingtip.FieldSales.Backoffice.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AccountController));
        private readonly ApplicationSignInManager _signIn;
        private readonly ApplicationUserManager _users;

        public AccountController(ApplicationSignInManager signIn, ApplicationUserManager users)
        {
            _signIn = signIn;
            _users = users;
        }

        [HttpGet]
        public ActionResult Login(string returnUrl)
        {
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _users.FindByNameAsync(model.UserName);
            if (user != null)
            {
                var roles = await _users.GetRolesAsync(user.Id);
                if (!roles.Contains(Roles.SalesManager) && !roles.Contains(Roles.Administrator))
                {
                    ModelState.AddModelError("", "Your account does not have access to the sales office. Use the Field Sales application.");
                    return View(model);
                }
            }

            var result = await _signIn.PasswordSignInAsync(model.UserName, model.Password, model.RememberMe, shouldLockout: true);
            switch (result)
            {
                case SignInStatus.Success:
                    Log.InfoFormat("{0} signed in from {1}", model.UserName, Request.UserHostAddress);
                    return Url.IsLocalUrl(model.ReturnUrl) ? (ActionResult)Redirect(model.ReturnUrl) : RedirectToAction("Index", "Home");
                case SignInStatus.LockedOut:
                    Log.WarnFormat("{0} is locked out", model.UserName);
                    ModelState.AddModelError("", "The account is locked for 15 minutes after too many failed attempts.");
                    return View(model);
                default:
                    Log.WarnFormat("Failed sign-in for {0} from {1}", model.UserName, Request.UserHostAddress);
                    ModelState.AddModelError("", "The user name or password is incorrect.");
                    return View(model);
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            HttpContext.GetOwinContext().Authentication.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            return RedirectToAction("Login");
        }
    }
}
