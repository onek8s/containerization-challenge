using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Proseware.ControlTower.Identity.Data;
using Proseware.ControlTower.Identity.Services;

namespace Proseware.ControlTower.Identity.Pages.Account;

public class ForgotPasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IEmailSender _mail;
    private readonly ILogger<ForgotPasswordModel> _logger;

    public ForgotPasswordModel(UserManager<ApplicationUser> users, IEmailSender mail, ILogger<ForgotPasswordModel> logger)
    {
        _users = users;
        _mail = mail;
        _logger = logger;
    }

    [BindProperty, Required, EmailAddress]
    public string Email { get; set; } = "";

    public bool Sent { get; private set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();
        var user = await _users.FindByEmailAsync(Email);
        if (user is not null && !user.Disabled)
        {
            var token = await _users.GeneratePasswordResetTokenAsync(user);
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var link = Url.Page("/Account/ResetPassword", null, new { userId = user.Id, code }, Request.Scheme)!;
            try
            {
                await _mail.SendAsync(Email, "Proseware Freight: reset your password",
                    $"<p>Hello {HtmlEncoder.Default.Encode(user.DisplayName)},</p><p>Reset your password by opening <a href=\"{HtmlEncoder.Default.Encode(link)}\">this link</a>. The link is valid for one day.</p><p>If you did not ask for this, ignore this message.</p>", ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Password reset mail to {Email} failed", Email);
            }
        }
        Sent = true;
        return Page();
    }
}
