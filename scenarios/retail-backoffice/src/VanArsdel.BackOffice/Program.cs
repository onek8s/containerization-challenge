using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using VanArsdel.BackOffice.Services;
using VanArsdel.BuildingBlocks.Hosting;

return VanArsdelHost.RunWeb("backoffice", args, BuildApp);

static WebApplication BuildApp(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseVanArsdelSerilog("backoffice");
    var configuration = builder.Configuration;

    // Keys on disk so cookies survive app pool recycles.
    var keys = configuration["DataProtection:KeysFolder"];
    var dataProtection = builder.Services.AddDataProtection().SetApplicationName("VanArsdel.BackOffice");
    if (!string.IsNullOrEmpty(keys))
    {
        dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keys));
    }

    // Staff sign in with Entra ID; store PCs that are still on the old domain use ADFS.
    var auth = builder.Services.AddAuthentication(o =>
    {
        o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        o.DefaultChallengeScheme = "Entra";
    }).AddCookie(o =>
    {
        o.Cookie.Name = "VanArsdel.BackOffice";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = false;
        o.AccessDeniedPath = "/denied";
    });
    foreach (var provider in configuration.GetSection("Oidc:Providers").GetChildren())
    {
        var name = provider["Name"];
        auth.AddOpenIdConnect(name, provider["DisplayName"] ?? name, o =>
        {
            o.Authority = provider["Authority"];
            o.ClientId = provider["ClientId"];
            o.ClientSecret = provider["ClientSecret"];
            o.ResponseType = OpenIdConnectResponseType.Code;
            o.UsePkce = true;
            o.SaveTokens = true;
            o.CallbackPath = "/signin-" + name.ToLowerInvariant();
            o.SignedOutCallbackPath = "/signout-callback-" + name.ToLowerInvariant();
            o.RequireHttpsMetadata = configuration.GetValue("Oidc:RequireHttpsMetadata", true);
            if (!o.RequireHttpsMetadata)
            {
                // Plain HTTP on developer machines: browsers drop SameSite=None cookies that are not Secure.
                o.CorrelationCookie.SameSite = SameSiteMode.Lax;
                o.NonceCookie.SameSite = SameSiteMode.Lax;
            }
            o.Scope.Clear();
            foreach (var scope in (provider["Scopes"] ?? "openid profile").Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                o.Scope.Add(scope);
            }
            o.TokenValidationParameters.NameClaimType = "name";
            o.TokenValidationParameters.RoleClaimType = provider["RoleClaimType"] ?? "roles";
            o.MapInboundClaims = false;
        });
    }
    builder.Services.AddAuthorization(o =>
    {
        o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    });

    builder.Services.AddRazorPages();
    builder.Services.AddServerSideBlazor();
    builder.Services.AddScoped<TokenProvider>();
    builder.Services.AddScoped<StoreLiveFeed>();
    builder.Services.AddHttpClient<RetailApi>(c =>
    {
        c.BaseAddress = new Uri(configuration["Gateway:BaseUrl"].TrimEnd('/') + "/");
        c.Timeout = TimeSpan.FromSeconds(60);
    });

    // One page with the health of every service, for the support team.
    builder.Services.AddHealthChecks();
    builder.Services.AddHealthChecksUI(s => s.SetEvaluationTimeInSeconds(60)).AddInMemoryStorage();

    var app = builder.Build();
    app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto });
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/error");
        app.UseHsts();
    }
    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapGet("/signin/{provider}", (string provider, string returnUrl) =>
        Results.Challenge(new AuthenticationProperties { RedirectUri = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl }, new[] { provider }))
        .AllowAnonymous();
    app.MapGet("/signout", async (HttpContext ctx) =>
    {
        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/signed-out");
    });
    app.MapGet("/signed-out", () => Results.Content("<p>You have signed out. <a href=\"/\">Sign in again</a></p>", "text/html")).AllowAnonymous();
    app.MapGet("/denied", () => Results.Content("<p>Your account has no back office role. Ask the service desk for access.</p>", "text/html")).AllowAnonymous();

    app.MapHealthChecks("/health/live").AllowAnonymous();
    app.MapHealthChecksUI(o => o.UIPath = "/health-ui");
    app.MapBlazorHub();
    app.MapFallbackToPage("/_Host");
    return app;
}
