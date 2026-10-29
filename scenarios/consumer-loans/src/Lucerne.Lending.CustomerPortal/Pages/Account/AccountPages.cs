using Lucerne.Lending.CustomerPortal.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Lucerne.Lending.CustomerPortal.Pages.Account;

internal static class AccountPages
{
    public static string SafeReturn(IUrlHelper url, string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && url.IsLocalUrl(returnUrl) ? returnUrl : "/";

    public static async Task TouchSignInAsync(SignInManager<PortalUser> signIn, string email)
    {
        var user = await signIn.UserManager.FindByEmailAsync(email);
        if (user == null) return;
        user.LastSignInUtc = DateTime.UtcNow;
        await signIn.UserManager.UpdateAsync(user);
    }
}
