using System.ComponentModel.DataAnnotations;
using IdentityServer4.Events;
using IdentityServer4.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Proseware.ControlTower.Identity.Data;

namespace Proseware.ControlTower.Identity.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IEventService _events;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users, IIdentityServerInteractionService interaction,
        IEventService events, IConfiguration configuration, ILogger<LoginModel> logger)
    {
        _signIn = signIn;
        _users = users;
        _interaction = interaction;
        _events = events;
        _configuration = configuration;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ClientName { get; private set; }

    public class InputModel
    {
        [Required]
        public string UserName { get; set; } = "";

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = "";

        public bool RememberLogin { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public async Task OnGetAsync(string? returnUrl)
    {
        Input.ReturnUrl = returnUrl;
        var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
        ClientName = context?.Client.ClientName;
        if (context?.LoginHint is { } hint) Input.UserName = hint;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var context = await _interaction.GetAuthorizationContextAsync(Input.ReturnUrl);
        ClientName = context?.Client.ClientName;
        if (!ModelState.IsValid) return Page();

        var user = await _users.FindByNameAsync(Input.UserName) ?? await _users.FindByEmailAsync(Input.UserName);
        if (user is null || user.Disabled)
        {
            await _events.RaiseAsync(new UserLoginFailureEvent(Input.UserName, "unknown or disabled user", clientId: context?.Client.ClientId));
            ModelState.AddModelError(string.Empty, "Invalid user name or password.");
            return Page();
        }

        var result = await _signIn.PasswordSignInAsync(user, Input.Password, Input.RememberLogin, lockoutOnFailure: true);
        if (result.RequiresTwoFactor)
            return RedirectToPage("./LoginWith2fa", new { returnUrl = Input.ReturnUrl, rememberMe = Input.RememberLogin });
        if (result.IsLockedOut)
        {
            _logger.LogWarning("User {User} locked out", user.UserName);
            return RedirectToPage("./Lockout");
        }
        if (!result.Succeeded)
        {
            await _events.RaiseAsync(new UserLoginFailureEvent(Input.UserName, "invalid credentials", clientId: context?.Client.ClientId));
            ModelState.AddModelError(string.Empty, "Invalid user name or password.");
            return Page();
        }

        user.LastLoginUtc = DateTime.UtcNow;
        await _users.UpdateAsync(user);
        await _events.RaiseAsync(new UserLoginSuccessEvent(user.UserName, user.Id, user.DisplayName, clientId: context?.Client.ClientId));

        // Staff must enrol an authenticator app before they can use the applications.
        if (user.IsStaff && !user.TwoFactorEnabled && _configuration.GetValue("IdentityServer:RequireStaffTwoFactor", true))
            return RedirectToPage("/Manage/EnableAuthenticator", new { returnUrl = Input.ReturnUrl, required = true });

        return RedirectAfterLogin(Input.ReturnUrl, context is not null);
    }

    internal IActionResult RedirectAfterLogin(string? returnUrl, bool fromAuthorizeRequest)
    {
        if (fromAuthorizeRequest || _interaction.IsValidReturnUrl(returnUrl) || Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl!);
        if (string.IsNullOrEmpty(returnUrl))
            return Redirect("~/");
        throw new InvalidOperationException("Invalid return URL.");
    }
}
