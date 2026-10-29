using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Proseware.ControlTower.Identity.Data;

namespace Proseware.ControlTower.Identity.Pages.Account;

public class ResetPasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;

    public ResetPasswordModel(UserManager<ApplicationUser> users) => _users = users;

    [BindProperty(SupportsGet = true), Required]
    public string UserId { get; set; } = "";

    [BindProperty(SupportsGet = true), Required]
    public string Code { get; set; } = "";

    [BindProperty, Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [BindProperty, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";

    public bool Done { get; private set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var user = await _users.FindByIdAsync(UserId);
        if (user is null)
        {
            Done = true; // do not reveal unknown users
            return Page();
        }
        var token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Code));
        var result = await _users.ResetPasswordAsync(user, token, Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return Page();
        }
        await _users.SetLockoutEndDateAsync(user, null);
        Done = true;
        return Page();
    }
}
