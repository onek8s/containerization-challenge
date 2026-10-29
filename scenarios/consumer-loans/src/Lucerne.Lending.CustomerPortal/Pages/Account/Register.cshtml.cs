using System.ComponentModel.DataAnnotations;
using Lucerne.Lending.CustomerPortal.Identity;
using Lucerne.Lending.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Lucerne.Lending.CustomerPortal.Pages.Account;

public sealed class RegisterModel : PageModel
{
    private readonly UserManager<PortalUser> _users;
    private readonly SignInManager<PortalUser> _signIn;
    private readonly ILogger<RegisterModel> _logger;

    public RegisterModel(UserManager<PortalUser> users, SignInManager<PortalUser> signIn, ILogger<RegisterModel> logger)
    {
        _users = users;
        _signIn = signIn;
        _logger = logger;
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public sealed class InputModel
    {
        [Required, StringLength(100)] public string FirstName { get; set; } = "";
        [Required, StringLength(100)] public string LastName { get; set; } = "";
        [Required] public string PersonalIdentityCode { get; set; } = "";
        [Required, EmailAddress] public string Email { get; set; } = "";
        [Phone] public string? Phone { get; set; }
        [Required, StringLength(128, MinimumLength = 10)] public string Password { get; set; } = "";
        [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")] public string ConfirmPassword { get; set; } = "";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var pic = Input.PersonalIdentityCode.Trim().ToUpperInvariant();
        if (!Domain.PersonalIdentityCode.IsValid(pic)) ModelState.AddModelError("Input.PersonalIdentityCode", "The personal identity code is not valid.");
        else if (Domain.PersonalIdentityCode.AgeOn(pic, DateOnly.FromDateTime(DateTime.Today)) < 18)
            ModelState.AddModelError("Input.PersonalIdentityCode", "The online loan service is available to adults only.");
        else if (await _users.Users.AnyAsync(u => u.PersonalIdentityCode == pic))
            ModelState.AddModelError("Input.PersonalIdentityCode", "An account already exists for this person. Please sign in or call customer service.");
        if (!ModelState.IsValid) return Page();

        var user = new PortalUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            PhoneNumber = Input.Phone,
            PersonalIdentityCode = pic,
            FirstName = Input.FirstName.Trim(),
            LastName = Input.LastName.Trim()
        };
        var result = await _users.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return Page();
        }
        _logger.LogInformation("Portal account created for {Pic}", Domain.PersonalIdentityCode.Mask(pic));
        await _signIn.SignInAsync(user, isPersistent: false);
        return RedirectToPage("Manage/TwoFactor", new { ReturnUrl });
    }
}
