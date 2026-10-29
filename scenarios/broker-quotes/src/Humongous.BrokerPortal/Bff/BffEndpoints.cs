using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Humongous.BrokerPortal.Bff;

public static class BffEndpoints
{
    public const string CsrfHeader = "X-CSRF";

    public static void MapBff(this WebApplication app)
    {
        var bff = app.MapGroup("/bff");

        bff.MapGet("/login", (string? returnUrl) =>
            Results.Challenge(new AuthenticationProperties { RedirectUri = SafeReturnUrl(returnUrl) },
                [OpenIdConnectDefaults.AuthenticationScheme]));

        bff.MapPost("/logout", () =>
            Results.SignOut(new AuthenticationProperties { RedirectUri = "/" },
                [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]))
            .RequireAuthorization();

        bff.MapGet("/user", (HttpContext context) =>
        {
            var user = context.User;
            if (user.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();
            return Results.Ok(new
            {
                name = user.FindFirst("name")?.Value,
                email = user.FindFirst("email")?.Value,
                agency = user.FindFirst("agency")?.Value,
                roles = user.FindAll("role").Select(c => c.Value).ToArray()
            });
        });
    }

    private static string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") ? returnUrl : "/";
}
