using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using log4net;
using Microsoft.AspNet.Identity;
using Wingtip.FieldSales.Backoffice.Models;
using Wingtip.FieldSales.Data;

namespace Wingtip.FieldSales.Backoffice.Controllers
{
    [Authorize(Roles = Roles.Administrator)]
    public class UsersController : Controller
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(UsersController));
        private readonly ApplicationUserManager _users;
        private readonly FieldSalesDbContext _db;

        public UsersController(ApplicationUserManager users, FieldSalesDbContext db)
        {
            _users = users;
            _db = db;
        }

        public async Task<ActionResult> Index()
        {
            var roleNames = await _db.Roles.ToDictionaryAsync(r => r.Id, r => r.Name);
            var users = await _db.Users.Include(u => u.Roles).OrderBy(u => u.UserName).ToListAsync();
            var rows = users.Select(u => new UserRow
            {
                Id = u.Id,
                UserName = u.UserName,
                DisplayName = u.DisplayName,
                RepCode = u.RepCode,
                DiscountLimitPercent = u.DiscountLimitPercent,
                Roles = string.Join(", ", u.Roles.Select(r => roleNames[r.RoleId]).OrderBy(n => n)),
                LockedOut = u.LockoutEndDateUtc.HasValue && u.LockoutEndDateUtc > System.DateTime.UtcNow
            }).ToList();
            return View(rows);
        }

        public ActionResult Create()
        {
            return View("Edit", new UserEditViewModel { IsSalesRep = true, DiscountLimitPercent = 5m });
        }

        public async Task<ActionResult> Edit(string id)
        {
            var user = await _users.FindByIdAsync(id);
            if (user == null) return HttpNotFound();
            var roles = await _users.GetRolesAsync(id);
            return View(new UserEditViewModel
            {
                Id = user.Id, UserName = user.UserName, DisplayName = user.DisplayName, Email = user.Email, RepCode = user.RepCode,
                DiscountLimitPercent = user.DiscountLimitPercent,
                IsSalesRep = roles.Contains(Roles.SalesRep),
                IsSalesManager = roles.Contains(Roles.SalesManager),
                IsAdministrator = roles.Contains(Roles.Administrator)
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> Save(UserEditViewModel model)
        {
            if (model.IsSalesRep && string.IsNullOrEmpty(model.RepCode)) ModelState.AddModelError("RepCode", "Sales reps need a rep code.");
            if (string.IsNullOrEmpty(model.Id) && string.IsNullOrEmpty(model.NewPassword)) ModelState.AddModelError("NewPassword", "Give the first password.");
            if (!string.IsNullOrEmpty(model.RepCode) && await _db.Users.AnyAsync(u => u.RepCode == model.RepCode && u.Id != model.Id))
                ModelState.AddModelError("RepCode", "Rep code " + model.RepCode + " is already used.");
            if (!ModelState.IsValid) return View("Edit", model);

            ApplicationUser user;
            IdentityResult result;
            if (string.IsNullOrEmpty(model.Id))
            {
                user = new ApplicationUser { UserName = model.UserName };
                Apply(model, user);
                result = await _users.CreateAsync(user, model.NewPassword);
            }
            else
            {
                user = await _users.FindByIdAsync(model.Id);
                if (user == null) return HttpNotFound();
                Apply(model, user);
                result = await _users.UpdateAsync(user);
                if (result.Succeeded && !string.IsNullOrEmpty(model.NewPassword))
                {
                    await _users.RemovePasswordAsync(user.Id);
                    result = await _users.AddPasswordAsync(user.Id, model.NewPassword);
                }
            }

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors) ModelState.AddModelError("", error);
                return View("Edit", model);
            }

            await SetRoleAsync(user.Id, Roles.SalesRep, model.IsSalesRep);
            await SetRoleAsync(user.Id, Roles.SalesManager, model.IsSalesManager);
            await SetRoleAsync(user.Id, Roles.Administrator, model.IsAdministrator);
            await _users.UpdateSecurityStampAsync(user.Id);

            Log.InfoFormat("{0} saved user {1} (rep {2}, limit {3} %)", User.Identity.Name, user.UserName, user.RepCode, user.DiscountLimitPercent);
            TempData["Message"] = "User " + user.UserName + " saved.";
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> Unlock(string id)
        {
            await _users.SetLockoutEndDateAsync(id, System.DateTimeOffset.UtcNow.AddMinutes(-1));
            await _users.ResetAccessFailedCountAsync(id);
            TempData["Message"] = "Account unlocked.";
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> SignOutEverywhere(string id)
        {
            var user = await _users.FindByIdAsync(id);
            if (user == null) return HttpNotFound();

            // Refresh tokens of the desktop client are removed; the current access token stays valid until it expires.
            _db.RefreshTokens.RemoveRange(_db.RefreshTokens.Where(t => t.UserName == user.UserName));
            await _db.SaveChangesAsync();
            await _users.UpdateSecurityStampAsync(id);

            TempData["Message"] = user.UserName + " has to sign in again on all devices.";
            return RedirectToAction("Index");
        }

        private static void Apply(UserEditViewModel model, ApplicationUser user)
        {
            user.DisplayName = model.DisplayName;
            user.Email = model.Email;
            user.RepCode = string.IsNullOrEmpty(model.RepCode) ? null : model.RepCode;
            user.DiscountLimitPercent = model.DiscountLimitPercent;
        }

        private async Task SetRoleAsync(string userId, string role, bool member)
        {
            var isMember = await _users.IsInRoleAsync(userId, role);
            if (member && !isMember) await _users.AddToRoleAsync(userId, role);
            if (!member && isMember) await _users.RemoveFromRoleAsync(userId, role);
        }
    }
}
