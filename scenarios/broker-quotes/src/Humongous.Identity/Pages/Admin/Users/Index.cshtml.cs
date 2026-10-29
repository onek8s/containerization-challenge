using Humongous.Identity.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Humongous.Identity.Pages.Admin.Users;

public class UsersModel(UserManager<ApplicationUser> users, ILogger<UsersModel> logger) : PageModel
{
    public record UserRow(string Id, string Name, string Email, string? Agency, IList<string> Roles, DateTime? LastSignIn, bool Locked);

    public sealed class NewUser
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Agency { get; set; }
        public string Role { get; set; } = Data.Roles.Broker;
        public string Password { get; set; } = "";
    }

    public List<UserRow> Users { get; } = [];
    [BindProperty] public NewUser Input { get; set; } = new();
    public string? Message { get; private set; }

    // Broker administrators manage the users of their own agency only.
    private string? AgencyScope => User.IsInRole(Roles.Administrator) ? null : User.FindFirst(CustomClaims.Agency)?.Value
        ?? users.GetUserAsync(User).Result?.AgencyCode;

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostCreateAsync()
    {
        var scope = AgencyScope;
        var user = new ApplicationUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            EmailConfirmed = true,
            DisplayName = Input.Name,
            AgencyCode = scope ?? (string.IsNullOrWhiteSpace(Input.Agency) ? null : Input.Agency.Trim().ToUpperInvariant())
        };
        var role = scope is null ? Input.Role : Roles.Broker;
        var result = await users.CreateAsync(user, Input.Password);
        if (result.Succeeded)
        {
            await users.AddToRoleAsync(user, role);
            logger.LogInformation("{Admin} created user {Email} ({Role}, agency {Agency})", User.Identity?.Name, user.Email, role, user.AgencyCode);
            Message = $"Created {user.Email}.";
        }
        else
        {
            Message = string.Join(" ", result.Errors.Select(e => e.Description));
        }
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostLockAsync(string id)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null || (AgencyScope is { } scope && user.AgencyCode != scope))
            return NotFound();

        var locked = await users.IsLockedOutAsync(user);
        await users.SetLockoutEnabledAsync(user, true);
        await users.SetLockoutEndDateAsync(user, locked ? null : DateTimeOffset.MaxValue);
        if (!locked) await users.UpdateSecurityStampAsync(user);
        logger.LogInformation("{Admin} {Action} user {Email}", User.Identity?.Name, locked ? "unlocked" : "locked", user.Email);
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var scope = AgencyScope;
        var query = users.Users.AsNoTracking();
        if (scope is not null) query = query.Where(u => u.AgencyCode == scope);
        foreach (var u in await query.OrderBy(u => u.AgencyCode).ThenBy(u => u.DisplayName).ToListAsync())
        {
            Users.Add(new UserRow(u.Id, u.DisplayName, u.Email ?? "", u.AgencyCode, await users.GetRolesAsync(u), u.LastSignInUtc,
                u.LockoutEnd is { } end && end > DateTimeOffset.UtcNow));
        }
    }
}
