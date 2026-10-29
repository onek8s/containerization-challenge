using Microsoft.AspNetCore.Authentication.Negotiate;
using Wingtip.Invoices.Admin.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
builder.Services.AddAuthorization(options =>
{
    var clerks = builder.Configuration["Roles:Clerks"];
    var admins = builder.Configuration["Roles:Admins"];
    options.AddPolicy(Policies.Clerk, p => p.RequireRole(clerks, admins));
    options.AddPolicy(Policies.Admin, p => p.RequireRole(admins));
    options.FallbackPolicy = options.GetPolicy(Policies.Clerk);
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Suppliers", Policies.Admin);
});
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<HubData>();
builder.Services.AddSingleton<UserDirectory>();
builder.Services.AddSingleton<ProcessorClient>();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddHttpContextAccessor();

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
