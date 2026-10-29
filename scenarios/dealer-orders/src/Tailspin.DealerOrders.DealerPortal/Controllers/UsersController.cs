using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using AutoMapper;
using log4net;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Tailspin.DealerOrders.Data;
using Tailspin.DealerOrders.DealerPortal.Infrastructure;
using Tailspin.DealerOrders.DealerPortal.Models;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.DealerPortal.Controllers
{
    /// <summary>Dealer administrators manage their own dealership's portal users.</summary>
    [PortalRole(PortalRoles.DealerAdmin)]
    public class UsersController : PortalController
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(UsersController));

        private readonly PasswordMail _passwordMail;
        private readonly IClock _clock;

        public UsersController(PasswordMail passwordMail, IClock clock)
        {
            _passwordMail = passwordMail;
            _clock = clock;
        }

        private PortalUserManager Users => HttpContext.GetOwinContext().GetUserManager<PortalUserManager>();

        public async Task<ActionResult> Index()
        {
            var users = Users.Users.Where(u => u.DealerId == DealerId).OrderBy(u => u.UserName).ToList();
            var models = Mapper.Map<UserViewModel[]>(users);
            foreach (var m in models)
            {
                m.Roles = (await Users.GetRolesAsync(m.Id)).ToList();
                m.IsLockedOut = await Users.IsLockedOutAsync(m.Id);
            }
            return View(models);
        }

        public ActionResult Create() => View("Edit", new UserViewModel { LanguageCode = "en", Roles = { PortalRoles.DealerSales } });

        public async Task<ActionResult> Edit(string id)
        {
            var user = await Own(id);
            if (user == null) return HttpNotFound();
            var model = Mapper.Map<UserViewModel>(user);
            model.Roles = (await Users.GetRolesAsync(id)).ToList();
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> Save(UserViewModel model, string[] roles)
        {
            if (!ModelState.IsValid) return View("Edit", model);
            roles = (roles ?? new string[0]).Where(r => PortalRoles.All.Contains(r)).ToArray();
            PortalUser user;
            if (string.IsNullOrEmpty(model.Id))
            {
                user = new PortalUser
                {
                    UserName = model.UserName.Trim(),
                    Email = model.Email.Trim(),
                    DisplayName = model.DisplayName,
                    LanguageCode = model.LanguageCode,
                    DealerId = DealerId,
                    CreatedUtc = _clock.UtcNow
                };
                // No password: the new user gets a reset link and chooses one.
                var created = await Users.CreateAsync(user);
                if (!created.Succeeded)
                {
                    foreach (var e in created.Errors) ModelState.AddModelError("", e);
                    return View("Edit", model);
                }
                await SendInvitation(user);
                Log.InfoFormat("{0} created portal user {1}", UserName, user.UserName);
            }
            else
            {
                user = await Own(model.Id);
                if (user == null) return HttpNotFound();
                if (user.Id == User.Identity.GetUserId() && !roles.Contains(PortalRoles.DealerAdmin))
                {
                    ModelState.AddModelError("", "You cannot remove your own administrator role.");
                    return View("Edit", model);
                }
                user.Email = model.Email.Trim();
                user.DisplayName = model.DisplayName;
                user.LanguageCode = model.LanguageCode;
                if (user.Disabled != model.Disabled)
                {
                    user.Disabled = model.Disabled;
                    await Users.UpdateSecurityStampAsync(user.Id);
                }
                await Users.UpdateAsync(user);
            }

            var current = await Users.GetRolesAsync(user.Id);
            await Users.RemoveFromRolesAsync(user.Id, current.Except(roles).ToArray());
            await Users.AddToRolesAsync(user.Id, roles.Except(current).ToArray());
            Flash($"User {user.UserName} saved.");
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> Unlock(string id)
        {
            if (await Own(id) == null) return HttpNotFound();
            await Users.SetLockoutEndDateAsync(id, DateTimeOffset.UtcNow);
            await Users.ResetAccessFailedCountAsync(id);
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> SendReset(string id)
        {
            var user = await Own(id);
            if (user == null) return HttpNotFound();
            await SendInvitation(user);
            Flash($"A password link was sent to {user.Email}.");
            return RedirectToAction("Index");
        }

        private async Task SendInvitation(PortalUser user)
        {
            var code = await Users.GeneratePasswordResetTokenAsync(user.Id);
            _passwordMail.Send(user, Url.Action("ResetPassword", "Account", new { userId = user.Id, code }, Request.Url.Scheme));
        }

        private async Task<PortalUser> Own(string id)
        {
            var user = await Users.FindByIdAsync(id);
            return user != null && user.DealerId == DealerId ? user : null;
        }
    }
}
