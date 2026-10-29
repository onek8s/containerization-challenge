using ContosoFoods.TradeRebates.Data;
using ContosoFoods.TradeRebates.Portal;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PortalOptions>(builder.Configuration.GetSection("Portal"));
var access = builder.Configuration.GetSection("Access").Get<AccessOptions>() ?? new AccessOptions();

builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
builder.Services.AddAuthorization(options =>
{
    static bool InAny(AuthorizationHandlerContext ctx, IEnumerable<string> groups) => groups.Any(ctx.User.IsInRole);

    options.AddPolicy(Policies.Analyst, p => p.RequireAuthenticatedUser()
        .RequireAssertion(ctx => InAny(ctx, access.Analysts.Concat(access.Approvers).Concat(access.Admins))));
    options.AddPolicy(Policies.Approver, p => p.RequireAuthenticatedUser()
        .RequireAssertion(ctx => InAny(ctx, access.Approvers.Concat(access.Admins))));
    options.AddPolicy(Policies.Admin, p => p.RequireAuthenticatedUser()
        .RequireAssertion(ctx => InAny(ctx, access.Admins)));
    options.FallbackPolicy = options.GetPolicy(Policies.Analyst);
});

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/Distributors", Policies.Admin);
    o.Conventions.AuthorizeFolder("/Audit", Policies.Admin);
});

builder.Services.AddSingleton<RebateDatabase>();
builder.Services.AddScoped<IPortalStore, SqlRebateStore>();

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
