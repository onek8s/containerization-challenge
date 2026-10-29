using System.Security.Claims;
using Humongous.Identity.Data;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Humongous.Identity.Controllers;

public class AuthorizationController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IOpenIddictScopeManager scopeManager,
    ILogger<AuthorizationController> logger) : Controller
{
    [HttpGet("~/connect/authorize"), HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        var result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!result.Succeeded || request.HasPrompt(Prompts.Login))
        {
            if (request.HasPrompt(Prompts.None))
                return Forbid(Error(Errors.LoginRequired, "The user is not signed in."), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            var returnUrl = Request.PathBase + Request.Path + QueryString.Create(
                Request.HasFormContentType ? Request.Form.ToList() : Request.Query.ToList());
            return Challenge(new AuthenticationProperties { RedirectUri = returnUrl }, IdentityConstants.ApplicationScheme);
        }

        var user = await userManager.GetUserAsync(result.Principal);
        if (user is null || await userManager.IsLockedOutAsync(user))
            return Forbid(Error(Errors.AccessDenied, "The account is not available."), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var principal = await CreatePrincipalAsync(user, request.GetScopes());
        user.LastSignInUtc = DateTime.UtcNow;
        await userManager.UpdateAsync(user);
        logger.LogInformation("Issued authorization code to {Client} for {User}", request.ClientId, user.Email);
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("~/connect/token"), IgnoreAntiforgeryToken, Produces("application/json")]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
            throw new InvalidOperationException("The specified grant type is not supported.");

        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var user = await userManager.FindByIdAsync(result.Principal?.GetClaim(Claims.Subject) ?? "");
        if (user is null || !await signInManager.CanSignInAsync(user) || await userManager.IsLockedOutAsync(user))
            return Forbid(Error(Errors.InvalidGrant, "The account is no longer allowed to sign in."), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        // Roles and agency are read again so a change made by an administrator applies at the next refresh.
        var principal = await CreatePrincipalAsync(user, result.Principal!.GetScopes());
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("~/connect/logout"), HttpPost("~/connect/logout"), IgnoreAntiforgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return SignOut(new AuthenticationProperties { RedirectUri = "/" }, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<ClaimsPrincipal> CreatePrincipalAsync(ApplicationUser user, IEnumerable<string> scopes)
    {
        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);

        identity.SetClaim(Claims.Subject, user.Id)
            .SetClaim(Claims.Email, user.Email)
            .SetClaim(Claims.Name, user.DisplayName)
            .SetClaim(Claims.PreferredUsername, user.UserName)
            .SetClaim(CustomClaims.Agency, user.AgencyCode)
            .SetClaims(Claims.Role, [.. await userManager.GetRolesAsync(user)]);

        identity.SetScopes(scopes);
        var resources = new List<string>();
        await foreach (var resource in scopeManager.ListResourcesAsync(identity.GetScopes()))
            resources.Add(resource);
        identity.SetResources(resources);
        identity.SetDestinations(GetDestinations);
        return new ClaimsPrincipal(identity);
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        switch (claim.Type)
        {
            case Claims.Name or Claims.PreferredUsername or Claims.Email or Claims.Role or CustomClaims.Agency:
                yield return Destinations.AccessToken;
                yield return Destinations.IdentityToken;
                yield break;
            case "AspNet.Identity.SecurityStamp":
                yield break;
            default:
                yield return Destinations.AccessToken;
                yield break;
        }
    }

    private static AuthenticationProperties Error(string error, string description) => new(new Dictionary<string, string?>
    {
        [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
    });
}
