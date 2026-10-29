using System.Web.Mvc;
using System.Web.Security;
using Northwind.Inspections.Core;
using Northwind.Inspections.Infrastructure.Data;
using Northwind.Inspections.Web.Models;
using Northwind.Inspections.Web.Services;

namespace Northwind.Inspections.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly AdjusterRepository adjusters = new AdjusterRepository(PortalData.Db);

        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            return View(new LoginModel { ReturnUrl = returnUrl });
        }

        [AllowAnonymous]
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Login(LoginModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var adjuster = adjusters.FindByLogin(model.Login.Trim());
            if (adjuster == null || !adjuster.IsActive || !PasswordHasher.Verify(model.Password, adjuster.PasswordHash))
            {
                if (adjuster != null)
                {
                    adjusters.RecordLogin(adjuster.Id, false);
                }
                ModelState.AddModelError(string.Empty, "The user name or password is incorrect, or the account is locked.");
                return View(model);
            }

            adjusters.RecordLogin(adjuster.Id, true);
            Session.Clear();
            Response.Cookies.Add(PortalUser.CreateCookie(adjuster, model.Remember));

            if (Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }
            return RedirectToAction("Index", "Inspections");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Abandon();
            return RedirectToAction("Login");
        }

        public ActionResult Password()
        {
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Password(string current, string password, string confirm)
        {
            var user = PortalUser.Current;
            var adjuster = adjusters.Get(user.Id);
            if (!PasswordHasher.Verify(current, adjuster.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "The current password is incorrect.");
            }
            else if (string.IsNullOrEmpty(password) || password.Length < 10 || password != confirm)
            {
                ModelState.AddModelError(string.Empty, "The new password must be at least 10 characters and match the confirmation.");
            }
            if (!ModelState.IsValid)
            {
                return View();
            }
            adjusters.SetPassword(adjuster.Id, PasswordHasher.Hash(password));
            PortalSession.AddNotice("Your password was changed.");
            return RedirectToAction("Index", "Inspections");
        }
    }
}
