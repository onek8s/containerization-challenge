using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Proseware.ControlTower.Identity.Data;

namespace Proseware.ControlTower.Identity.Pages.Manage;

public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;

    public IndexModel(UserManager<ApplicationUser> users) => _users = users;

    public string UserName { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public bool TwoFactorEnabled { get; private set; }
    public IList<string> UserRoles { get; private set; } = Array.Empty<string>();

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Challenge();
        UserName = user.UserName;
        DisplayName = user.DisplayName;
        TwoFactorEnabled = user.TwoFactorEnabled;
        UserRoles = await _users.GetRolesAsync(user);
        return Page();
    }
}
