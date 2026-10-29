using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Proseware.ControlTower.Identity.Data;

namespace Proseware.ControlTower.Identity.Pages.Admin;

public class UsersModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly AuthDbContext _db;
    private readonly ILogger<UsersModel> _logger;

    public UsersModel(UserManager<ApplicationUser> users, AuthDbContext db, ILogger<UsersModel> logger)
    {
        _users = users;
        _db = db;
        _logger = logger;
    }

    public record UserRow(string Id, string UserName, string DisplayName, string? Email, string Roles, int? CustomerId, bool TwoFactor, bool Disabled, DateTime? LastLogin);

    public class NewUserInput
    {
        [Required, StringLength(64)]
        public string UserName { get; set; } = "";

        [Required, StringLength(100)]
        public string DisplayName { get; set; } = "";

        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        public string Role { get; set; } = Data.Roles.Customer;

        public int? CustomerId { get; set; }

        [Required, MinLength(10)]
        public string Password { get; set; } = "";
    }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Query { get; set; }

    [BindProperty]
    public NewUserInput NewUser { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public IReadOnlyList<UserRow> Users { get; private set; } = Array.Empty<UserRow>();

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (NewUser.Role == Data.Roles.Customer && NewUser.CustomerId is null)
            ModelState.AddModelError("NewUser.CustomerId", "Portal users need a customer id.");
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }
        var user = new ApplicationUser
        {
            UserName = NewUser.UserName.Trim(),
            DisplayName = NewUser.DisplayName.Trim(),
            Email = NewUser.Email,
            EmailConfirmed = NewUser.Email is not null,
            CustomerId = NewUser.Role == Data.Roles.Customer ? NewUser.CustomerId : null,
            IsStaff = NewUser.Role != Data.Roles.Customer,
            CreatedUtc = DateTime.UtcNow
        };
        var result = await _users.CreateAsync(user, NewUser.Password);
        if (result.Succeeded) result = await _users.AddToRoleAsync(user, NewUser.Role);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            await LoadAsync();
            return Page();
        }
        _logger.LogInformation("{Admin} created user {User} ({Role})", User.Identity?.Name, user.UserName, NewUser.Role);
        StatusMessage = $"User {user.UserName} created.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleDisabledAsync(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();
        user.Disabled = !user.Disabled;
        await _users.UpdateAsync(user);
        await _users.UpdateSecurityStampAsync(user);
        _logger.LogInformation("{Admin} set user {User} disabled={Disabled}", User.Identity?.Name, user.UserName, user.Disabled);
        StatusMessage = $"User {user.UserName} {(user.Disabled ? "disabled" : "enabled")}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostReset2faAsync(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();
        await _users.SetTwoFactorEnabledAsync(user, false);
        await _users.ResetAuthenticatorKeyAsync(user);
        _logger.LogInformation("{Admin} reset two-factor of {User}", User.Identity?.Name, user.UserName);
        StatusMessage = $"Authenticator of {user.UserName} reset; the user sets it up again at next sign-in.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var q = _users.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(Query))
            q = q.Where(u => u.UserName.Contains(Query) || u.DisplayName.Contains(Query) || (u.Email != null && u.Email.Contains(Query)));
        var users = await q.OrderBy(u => u.UserName).Take(200).ToListAsync();
        var ids = users.Select(u => u.Id).ToList();
        var roles = await (from ur in _db.UserRoles
                           join r in _db.Roles on ur.RoleId equals r.Id
                           where ids.Contains(ur.UserId)
                           select new { ur.UserId, r.Name }).ToListAsync();
        Users = users.Select(u => new UserRow(u.Id, u.UserName, u.DisplayName, u.Email,
            string.Join(", ", roles.Where(r => r.UserId == u.Id).Select(r => r.Name).OrderBy(n => n)),
            u.CustomerId, u.TwoFactorEnabled, u.Disabled, u.LastLoginUtc)).ToList();
    }
}
