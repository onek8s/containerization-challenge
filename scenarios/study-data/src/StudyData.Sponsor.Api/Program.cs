using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Serilog;
using StudyData.Data;
using StudyData.Data.Seed;
using StudyData.Sponsor.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, log) => log.ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(context.Configuration["Logging:LogFile"] ?? "logs/sponsor-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 90));

builder.Services.AddStudyData(builder.Configuration);
builder.Services.AddSingleton<StudyData.Core.Services.IDerivationService, ReadOnlyDerivation>();
builder.Services.AddScoped<SponsorAccess>();

var sponsor = builder.Configuration.GetSection("Sponsor");
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAuthentication(DevelopmentClientHandler.Scheme)
        .AddScheme<AuthenticationSchemeOptions, DevelopmentClientHandler>(DevelopmentClientHandler.Scheme, null);
}
else
{
    // ADFS application group: the sponsor system uses the client credentials grant with a certificate.
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.Authority = sponsor["Authority"];
            o.Audience = sponsor["Audience"];
            o.MapInboundClaims = false;
        });
}

builder.Services.AddAuthorization(o => o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "Study Data sponsor API", Version = "v1", Description = "Enrolment status, data exports and safety reports for sponsors of studies run by Fabrikam Clinical Research." });
    o.AddSecurityDefinition("bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "bearer" } }] = Array.Empty<string>()
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    StudyData.Data.ServiceCollectionExtensions.EnsureDataDirectory(builder.Configuration.GetConnectionString("StudyData")!);
    await DbInitializer.EnsureCreatedAsync(app.Services, seedSampleData: false);
}

app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();
app.MapGroup("/api/v1").MapSponsorApi();
app.Run();

public partial class Program;

namespace StudyData.Sponsor.Api
{
    /// <summary>Development: the calling client is named in the X-Client-Id header instead of a token.</summary>
    public class DevelopmentClientHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public new const string Scheme = "Development";

        public DevelopmentClientHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var client = Request.Headers["X-Client-Id"].FirstOrDefault();
            if (string.IsNullOrEmpty(client))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(new[] { new Claim("appid", client) }, Scheme);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }

    public sealed class ReadOnlyDerivation : StudyData.Core.Services.IDerivationService
    {
        public Task<StudyData.Core.Services.DerivationResult> DeriveAsync(StudyData.Core.Services.DerivationRequest request, CancellationToken cancellationToken = default) =>
            throw new StudyData.Core.Services.DerivationException("The sponsor API is read-only.");
    }
}
