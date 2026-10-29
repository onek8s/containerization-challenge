using Coho.Billing.Lookup.Services;
using Microsoft.AspNetCore.Authentication.Negotiate;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
builder.Services.AddAuthorization(options =>
{
    var groups = builder.Configuration.GetSection("Lookup:AllowedGroups").Get<string[]>() ?? Array.Empty<string>();
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireAssertion(context => groups.Length == 0 || groups.Any(g => context.User.IsInRole(g)))
        .Build();
});

builder.Services.AddRazorPages();
builder.Services.AddSingleton<BillingQueries>();
builder.Services.AddHttpClient<EngineClient>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["Lookup:EngineUrl"]);
        client.Timeout = TimeSpan.FromSeconds(20);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseDefaultCredentials = true });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
