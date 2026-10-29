using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using log4net;
using VanArsdel.Integration.Data;
using VanArsdel.Integration.Tracking.Models;
using VanArsdel.Integration.Tracking.Security;

namespace VanArsdel.Integration.Tracking.Controllers
{
    [Authorize(Roles = TrackingRoles.Admin)]
    public class UsersController : TrackingController
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(UsersController));
        private readonly IPortalUserStore _users;
        private readonly PasswordHasher _hasher;
        private readonly PartnerRepository _partners;

        public UsersController(IPortalUserStore users, PasswordHasher hasher, PartnerRepository partners)
        {
            _users = users;
            _hasher = hasher;
            _partners = partners;
        }

        public ActionResult Index()
        {
            return View(_users.All());
        }

        public ActionResult Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return View(new UserEditModel { IsNew = true });
            }

            var user = _users.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            var roles = user.RoleList;
            return View(new UserEditModel
            {
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                Email = user.Email,
                PartnerId = user.PartnerId,
                CustomerService = roles.Contains(TrackingRoles.CustomerService),
                Admin = roles.Contains(TrackingRoles.Admin),
                Locked = user.Locked
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(UserEditModel model)
        {
            var existing = model.IsNew ? null : _users.Find(model.UserName);
            if (model.IsNew && _users.Find(model.UserName) != null)
            {
                ModelState.AddModelError("UserName", "A user with this name already exists.");
            }

            var partnerId = string.IsNullOrWhiteSpace(model.PartnerId) ? null : model.PartnerId.Trim().ToUpperInvariant();
            if (partnerId != null && !_partners.LoadPartners().Any(p => p.PartnerId == partnerId))
            {
                ModelState.AddModelError("PartnerId", "Unknown partner.");
            }

            if (partnerId != null && (model.CustomerService || model.Admin))
            {
                ModelState.AddModelError(string.Empty, "Partner users cannot have internal roles.");
            }

            if ((model.IsNew || !string.IsNullOrEmpty(model.InitialPassword)) && (model.InitialPassword ?? string.Empty).Length < 10)
            {
                ModelState.AddModelError("InitialPassword", "Give an initial password of at least 10 characters.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var roles = new List<string>();
            if (partnerId != null)
            {
                roles.Add(TrackingRoles.Partner);
            }

            if (model.CustomerService)
            {
                roles.Add(TrackingRoles.CustomerService);
            }

            if (model.Admin)
            {
                roles.Add(TrackingRoles.Admin);
            }

            var user = existing ?? new PortalUser { UserName = model.UserName.Trim() };
            user.DisplayName = model.DisplayName.Trim();
            user.Email = model.Email.Trim();
            user.PartnerId = partnerId;
            user.Roles = string.Join(",", roles);
            user.Locked = model.Locked;
            if (!string.IsNullOrEmpty(model.InitialPassword))
            {
                user.PasswordHash = _hasher.Hash(model.InitialPassword);
                user.MustChangePassword = true;
            }

            _users.Save(user, User.Identity.Name);
            Log.InfoFormat("User {0} {1} by {2} (partner {3}, roles {4})", user.UserName, existing == null ? "created" : "updated", User.Identity.Name, partnerId ?? "-", user.Roles);
            TempData["Message"] = "Saved " + user.UserName + ".";
            return RedirectToAction("Index");
        }
    }
}
