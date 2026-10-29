using Humongous.Workbench.Components;
using Humongous.Workbench.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Polly;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) => logger.ReadFrom.Configuration(context.Configuration));

var oidc = builder.Configuration.GetSection("Oidc");
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "hq.workbench";
        options.ExpireTimeSpan = TimeSpan.FromHours(10);
        options.AccessDeniedPath = "/denied";
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = oidc["Authority"];
        options.ClientId = oidc["ClientId"];
        options.ClientSecret = oidc["ClientSecret"];
        options.RequireHttpsMetadata = oidc.GetValue("RequireHttpsMetadata", true);
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = false;
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = "role";
        options.Scope.Clear();
        foreach (var scope in new[] { "openid", "profile", "email", "roles", "offline_access", "quoting_api" })
            options.Scope.Add(scope);
        if (!oidc.GetValue("RequireHttpsMetadata", true))
        {
            options.NonceCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.NonceCookie.SameSite = SameSiteMode.Lax;
            options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        }
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Underwriter", p => p.RequireAuthenticatedUser().RequireRole("Underwriter", "SeniorUnderwriter"))
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireRole("Underwriter", "SeniorUnderwriter").Build());
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddHttpClient("oidc");
builder.Services.AddScoped<UserTokens>();
builder.Services.AddScoped<LiveUpdates>();
builder.Services.AddHttpClient<QuoteApiClient>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["QuoteApi:BaseAddress"]!);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(300 * attempt)));

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseSerilogRequestLogging();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Downloads go through the workbench so the browser never holds an API token.
app.MapGet("/files/{kind}/{id:guid}", async (string kind, Guid id, HttpContext context, IHttpClientFactory factory, IConfiguration config) =>
{
    var path = kind switch
    {
        "documents" => $"api/documents/{id}",
        "bordereaux" => $"api/bordereaux/{id}/file",
        _ => null
    };
    if (path is null) return Results.NotFound();
    var token = await context.GetTokenAsync("access_token");
    var client = factory.CreateClient();
    client.BaseAddress = new Uri(config["QuoteApi:BaseAddress"]!);
    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    var response = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
    if (!response.IsSuccessStatusCode) return Results.StatusCode((int)response.StatusCode);
    var fileName = response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName ?? $"{id}";
    return Results.Stream(await response.Content.ReadAsStreamAsync(), response.Content.Headers.ContentType?.ToString(), fileName.Trim('"'));
});

app.MapPost("/signout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties { RedirectUri = "/" });
});
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
