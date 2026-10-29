using Microsoft.AspNetCore.Identity;
using Serilog;
using StudyData.Calculation;
using StudyData.Data;
using StudyData.Data.Seed;
using StudyData.Edc.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, log) =>
{
    log.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(context.Configuration["Logging:LogFile"] ?? "logs/edc-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 90);
    if (OperatingSystem.IsWindows() && !context.HostingEnvironment.IsDevelopment())
    {
        log.WriteTo.EventLog("StudyData EDC", manageEventSource: false, restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning);
    }
});

builder.Services.Configure<EdcOptions>(builder.Configuration.GetSection(EdcOptions.Section));
builder.Services.AddStudyData(builder.Configuration);
builder.Services.AddStudyCalculation(builder.Configuration);

builder.Services
    .AddIdentity<SiteUser, IdentityRole>(o =>
    {
        o.Password.RequiredLength = 10;
        o.Password.RequireNonAlphanumeric = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
        o.User.RequireUniqueEmail = true;
        o.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<StudyDataContext>()
    .AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<SiteClaimsFactory>();

var sessionMinutes = builder.Configuration.GetValue("Edc:SessionMinutes", 30);
builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/account/login";
    o.AccessDeniedPath = "/account/denied";
    o.Cookie.Name = "StudyData.Edc";
    o.ExpireTimeSpan = TimeSpan.FromMinutes(sessionMinutes);
    o.SlidingExpiration = true;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<SiteAccess>();
builder.Services.AddSingleton<SourceDocumentVault>();
builder.Services.AddControllersWithViews(o => o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddHealthChecks().AddDbContextCheck<StudyDataContext>();

var app = builder.Build();

if (args.Length > 0 && args[0] is "setup-sample-study" or "add-site-user")
{
    // Administration verbs, run from the site folder with the production connection string:
    //   StudyData.Edc.Web.exe setup-sample-study
    //   StudyData.Edc.Web.exe add-site-user <email> "<full name>" <study> <site> <Investigator|Coordinator|...> [--sign]
    await AdminCommands.RunAsync(app.Services, args);
    return;
}

if (app.Environment.IsDevelopment())
{
    var connection = builder.Configuration.GetConnectionString("StudyData")!;
    StudyData.Data.ServiceCollectionExtensions.EnsureDataDirectory(connection);
    await DbInitializer.EnsureCreatedAsync(app.Services, builder.Configuration.GetValue("Edc:SeedSampleData", false));
}
else
{
    app.UseExceptionHandler("/home/error");
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();

public partial class Program;
