using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Litware.OrderHub.Portal.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Litware.OrderHub.Portal.Pages.Users
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<IdentityUser> _users;

        public IndexModel(UserManager<IdentityUser> users)
        {
            _users = users;
        }

        public IList<UserRow> Users { get; private set; }

        [BindProperty] public UserInput Input { get; set; }

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadAsync();
                return Page();
            }

            var user = await _users.FindByNameAsync(Input.UserName);
            IdentityResult result;
            if (user == null)
            {
                user = new IdentityUser(Input.UserName);
                result = await _users.CreateAsync(user, Input.Password);
            }
            else
            {
                var token = await _users.GeneratePasswordResetTokenAsync(user);
                result = await _users.ResetPasswordAsync(user, token, Input.Password);
            }

            if (result.Succeeded)
            {
                var current = await _users.GetRolesAsync(user);
                await _users.RemoveFromRolesAsync(user, current.Where(r => r != Input.Role));
                if (!current.Contains(Input.Role)) result = await _users.AddToRoleAsync(user, Input.Role);
            }

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
                await LoadAsync();
                return Page();
            }

            Log.Information("{Admin} saved user {User} as {Role}", User.Identity.Name, Input.UserName, Input.Role);
            TempData["Message"] = "User " + Input.UserName + " saved.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUnlockAsync(string id)
        {
            var user = await _users.FindByIdAsync(id);
            if (user == null) return NotFound();
            await _users.SetLockoutEndDateAsync(user, null);
            await _users.ResetAccessFailedCountAsync(user);
            return RedirectToPage();
        }

        private async Task LoadAsync()
        {
            var rows = new List<UserRow>();
            foreach (var user in await _users.Users.OrderBy(u => u.UserName).ToListAsync())
            {
                rows.Add(new UserRow
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    LockoutEnd = user.LockoutEnd > DateTimeOffset.UtcNow ? user.LockoutEnd : null,
                    Roles = await _users.GetRolesAsync(user)
                });
            }
            Users = rows;
        }

        public class UserRow
        {
            public string Id { get; set; }
            public string UserName { get; set; }
            public DateTimeOffset? LockoutEnd { get; set; }
            public IList<string> Roles { get; set; }
        }

        public class UserInput
        {
            [Required, StringLength(60)] public string UserName { get; set; }
            [Required, StringLength(100, MinimumLength = 10)] public string Password { get; set; }
            [Required] public string Role { get; set; } = Roles.Operator;
        }
    }
}
