using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serilog;

namespace Litware.OrderHub.Portal.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signIn;

        public LoginModel(SignInManager<IdentityUser> signIn)
        {
            _signIn = signIn;
        }

        [BindProperty, Required] public string UserName { get; set; }
        [BindProperty, Required] public string Password { get; set; }
        [BindProperty(SupportsGet = true)] public string ReturnUrl { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var result = await _signIn.PasswordSignInAsync(UserName, Password, isPersistent: false, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                Log.Information("User {User} signed in", UserName);
                return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/");
            }

            Log.Warning("Sign-in failed for {User} (locked out: {LockedOut})", UserName, result.IsLockedOut);
            ModelState.AddModelError(string.Empty, result.IsLockedOut ? "The account is locked. Try again in 15 minutes." : "Wrong user name or password.");
            return Page();
        }
    }
}
