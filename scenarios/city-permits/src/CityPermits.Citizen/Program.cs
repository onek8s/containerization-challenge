using System.Security.Claims;
using System.Text.Json;
using CityPermits.Archive;
using CityPermits.Citizen.Services;
using CityPermits.Contracts;
using CityPermits.Data;
using CityPermits.Domain.Model;
using CityPermits.Integration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Sustainsys.Saml2;
using Sustainsys.Saml2.Metadata;
using Sustainsys.Saml2.AspNetCore2;
using Sustainsys.Saml2.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));
var cfg = builder.Configuration;

builder.Services.AddPermitsData(cfg);
builder.Services.AddDocumentArchive(cfg);
builder.Services.AddScoped<CitizenUser>();
builder.Services.AddScoped<CaseService>();
builder.Services.AddSingleton(cfg.GetSection("Paystream").Get<PaystreamOptions>() ?? new());
builder.Services.AddSingleton<PaystreamClient>();
var addr = cfg.GetSection("AddressRegister").Get<AddressRegisterOptions>() ?? new();
builder.Services.AddSingleton<IAddressRegister>(addr.Mode == "File"
    ? new AddressRegisterFile(Path.Combine(builder.Environment.ContentRootPath, addr.FilePath))
    : new AddressRegisterApi(addr));
builder.Services.AddHttpClient<MapServiceClient>(c =>
{
    c.BaseAddress = new Uri(cfg["MapService:BaseUrl"] ?? "https://maps.corp.local/");
    c.DefaultRequestHeaders.Add("X-Map-Key", cfg["MapService:Key"]);
});

builder.Services.AddDataProtection().SetApplicationName("CityPermits.Citizen")
    .PersistKeysToFileSystem(new DirectoryInfo(cfg["DataProtection:KeyFolder"] ?? "App_Data/keys"));

var auth = builder.Services.AddAuthentication(o =>
{
    o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = cfg["Eid:Mode"] == "Development" ? CookieAuthenticationDefaults.AuthenticationScheme : Saml2Defaults.Scheme;
}).AddCookie(o =>
{
    o.LoginPath = "/SignIn";
    o.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    o.SlidingExpiration = true;
    o.Cookie.Name = "ashford.permits";
});
if (cfg["Eid:Mode"] != "Development")
{
    auth.AddSaml2(o =>
    {
        o.SPOptions.EntityId = new EntityId(cfg["Eid:EntityId"]);
        o.SPOptions.ReturnUrl = new Uri(cfg["Eid:ReturnUrl"]!);
        o.SPOptions.ServiceCertificates.Add(new ServiceCertificate
        {
            Certificate = LoadCertificate(cfg["Eid:SigningCertificateThumbprint"]!),
            Use = CertificateUse.Both
        });
        o.IdentityProviders.Add(new IdentityProvider(new EntityId(cfg["Eid:IdpEntityId"]), o.SPOptions)
        {
            MetadataLocation = cfg["Eid:IdpMetadata"],
            LoadMetadata = true,
            AllowUnsolicitedAuthnResponse = false
        });
    });
}

builder.Services.AddAuthorization();
builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/Cases");
    o.Conventions.AuthorizePage("/Apply");
});

var app = builder.Build();
app.UseSerilogRequestLogging();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Error"); app.UseHsts(); }
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.MapGet("/address-search", (string q, IAddressRegister reg) => Results.Ok(q.Length < 3 ? Array.Empty<AddressMatch>() : reg.Search(q, 15)));

// Paystream server-to-server notification.
app.MapPost("/payments/notify", async (HttpRequest req, PaystreamClient paystream, PermitsDbContext db, CaseService cases, ILogger<Program> log) =>
{
    using var reader = new StreamReader(req.Body);
    var body = await reader.ReadToEndAsync();
    var valid = paystream.VerifySignature(req.Headers["Paystream-Signature"].ToString(), body, DateTimeOffset.UtcNow);
    var n = JsonSerializer.Deserialize<PaystreamNotification>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    if (n is null) return Results.BadRequest();
    if (await db.PaymentNotifications.AnyAsync(x => x.EventId == n.EventId)) return Results.Ok();
    var record = new PaymentNotification { EventId = n.EventId, Body = body, ReceivedUtc = DateTime.UtcNow, SignatureValid = valid };
    db.PaymentNotifications.Add(record);
    if (!valid) { record.Outcome = "rejected: signature"; await db.SaveChangesAsync(); return Results.Unauthorized(); }

    var payment = await db.Payments.FirstOrDefaultAsync(p => p.ProviderReference == n.Payment.PaymentId);
    if (payment is null) { record.Outcome = "unknown payment"; await db.SaveChangesAsync(); return Results.Ok(); }
    var c = await db.Cases.Include(x => x.Fees).Include(x => x.Payments).Include(x => x.Events).FirstAsync(x => x.Id == payment.CaseId);
    payment.CardBrand = n.Payment.CardBrand;
    payment.Last4 = n.Payment.Last4;
    switch (n.EventType)
    {
        case "payment.authorised":
        case "payment.captured":
            await cases.MarkPaidAsync(payment, c);
            break;
        case "payment.failed":
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = n.Payment.FailureCode;
            break;
    }
    record.Outcome = n.EventType;
    await db.SaveChangesAsync();
    log.LogInformation("Paystream {Event} for {Case}", n.EventType, c.Reference);
    return Results.Ok();
});

app.MapGet("/documents/{id:int}", async (int id, ClaimsPrincipal user, PermitsDbContext db, CitizenUser cu, IDocumentArchive archive) =>
{
    var me = await cu.CurrentAsync(user);
    var doc = await db.CaseDocuments.FirstOrDefaultAsync(d => d.Id == id && d.VisibleToApplicant);
    if (doc is null || !await db.Cases.AnyAsync(c => c.Id == doc.CaseId && c.ApplicantId == me.Id)) return Results.NotFound();
    return Results.Stream(await archive.OpenReadAsync(doc.ArchiveKey), doc.ContentType, doc.FileName);
}).RequireAuthorization();

app.MapPost("/signout", async (HttpContext ctx) => { await ctx.SignOutAsync(); return Results.Redirect("/"); });

app.Run();

static System.Security.Cryptography.X509Certificates.X509Certificate2 LoadCertificate(string thumbprint)
{
    using var store = new System.Security.Cryptography.X509Certificates.X509Store(
        System.Security.Cryptography.X509Certificates.StoreName.My, System.Security.Cryptography.X509Certificates.StoreLocation.LocalMachine);
    store.Open(System.Security.Cryptography.X509Certificates.OpenFlags.ReadOnly);
    return store.Certificates.Find(System.Security.Cryptography.X509Certificates.X509FindType.FindByThumbprint, thumbprint, false)
        .OfType<System.Security.Cryptography.X509Certificates.X509Certificate2>().FirstOrDefault()
        ?? throw new InvalidOperationException($"eID signing certificate {thumbprint} not found in LocalMachine\\My.");
}

public partial class Program { }
