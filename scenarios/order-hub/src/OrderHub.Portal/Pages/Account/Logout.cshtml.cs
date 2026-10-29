using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Litware.OrderHub.Portal.Pages.Account
{
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signIn;

        public LogoutModel(SignInManager<IdentityUser> signIn)
        {
            _signIn = signIn;
        }

        public IActionResult OnGet() => RedirectToPage("/Index");

        public async Task<IActionResult> OnPostAsync()
        {
            await _signIn.SignOutAsync();
            return RedirectToPage("/Account/Login");
        }
    }
}
