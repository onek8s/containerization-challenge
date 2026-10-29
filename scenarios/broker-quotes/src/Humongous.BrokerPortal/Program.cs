using Humongous.BrokerPortal.Bff;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Serilog;
using Yarp.ReverseProxy.Transforms;

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
        options.Cookie.Name = "hq.portal";
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromHours(10);
        options.SlidingExpiration = true;
        // API style answers for the SPA instead of redirects.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
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
    .AddPolicy("Broker", p => p.RequireAuthenticatedUser().RequireRole("Broker"));

builder.Services.AddHttpClient("oidc");
builder.Services.AddSingleton<TokenStore>();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(context =>
    {
        context.AddRequestTransform(async transform =>
        {
            var tokens = transform.HttpContext.RequestServices.GetRequiredService<TokenStore>();
            var token = await tokens.GetAccessTokenAsync(transform.HttpContext);
            transform.ProxyRequest.Headers.Remove("Cookie");
            if (token is not null)
                transform.ProxyRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        });
    });

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
    app.UseHsts();
app.UseSerilogRequestLogging();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseWebSockets();

// Calls from the SPA must carry the X-CSRF header: a cross-site form post cannot set it.
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if ((path.StartsWithSegments("/api") || path.StartsWithSegments("/bff/logout"))
        && !HttpMethods.IsGet(context.Request.Method)
        && !context.Request.Headers.ContainsKey(BffEndpoints.CsrfHeader))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }
    await next();
});

app.MapBff();
app.MapReverseProxy().RequireAuthorization("Broker");
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapFallbackToFile("index.html");

app.Run();
