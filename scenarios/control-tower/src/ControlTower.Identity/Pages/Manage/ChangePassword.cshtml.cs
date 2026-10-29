using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Proseware.ControlTower.Identity.Data;

namespace Proseware.ControlTower.Identity.Pages.Manage;

public class ChangePasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;

    public ChangePasswordModel(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn)
    {
        _users = users;
        _signIn = signIn;
    }

    [BindProperty, Required, DataType(DataType.Password)]
    public string OldPassword { get; set; } = "";

    [BindProperty, Required, DataType(DataType.Password)]
    public string NewPassword { get; set; } = "";

    [BindProperty, DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";

    public bool Changed { get; private set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var user = await _users.GetUserAsync(User);
        if (user is null) return Challenge();
        var result = await _users.ChangePasswordAsync(user, OldPassword, NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return Page();
        }
        await _signIn.RefreshSignInAsync(user);
        Changed = true;
        return Page();
    }
}
