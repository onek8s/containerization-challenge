using System.Text.Json.Serialization;
using Humongous.Quoting.Api.Consumers;
using Humongous.Quoting.Api.Endpoints;
using Humongous.Quoting.Api.Hubs;
using Humongous.Quoting.Api.Infrastructure;
using Humongous.Quoting.Api.Services;
using Humongous.Quoting.Contracts;
using Humongous.Quoting.Data;
using Humongous.Quoting.Messaging;
using Humongous.Quoting.Rating;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) => logger.ReadFrom.Configuration(context.Configuration));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddQuotingData(builder.Configuration);
builder.Services.AddRating(builder.Configuration);
builder.Services.AddQuotingBus(builder.Configuration, bus => bus.AddConsumer<HubRelayConsumer, HubRelayConsumerDefinition>());

builder.Services.AddSingleton<RatingQueue>();
builder.Services.AddHostedService<RatingWorker>();
builder.Services.AddScoped<QuoteRater>();
builder.Services.AddScoped<QuoteService>();
builder.Services.AddSingleton<HubNotifier>();

builder.Services.ConfigureHttpJsonOptions(options => QuotingJson.Configure(options.SerializerOptions));
builder.Services.AddSignalR().AddJsonProtocol(options => QuotingJson.Configure(options.PayloadSerializerOptions));

var auth = builder.Configuration.GetSection("Authentication");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = auth["Authority"];
        options.Audience = auth["Audience"];
        options.RequireHttpsMetadata = auth.GetValue("RequireHttpsMetadata", true);
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = "role";
        options.Events = new JwtBearerEvents
        {
            // Browsers cannot set headers on the WebSocket request; SignalR sends the token in the query string.
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.AnyUser, p => p.RequireAuthenticatedUser().RequireClaim("scope"))
    .AddPolicy(Policies.Broker, p => p.RequireRole("Broker"))
    .AddPolicy(Policies.Underwriter, p => p.RequireRole("Underwriter", "SeniorUnderwriter"))
    .AddPolicy(Policies.SeniorUnderwriter, p => p.RequireRole("SeniorUnderwriter"))
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddHealthChecks()
    .AddDbContextCheck<QuotingDbContext>("database")
    .AddCheck<RatingEngineHealthCheck>("rating");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Humongous Broker Quotes API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
    });
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitialiseAsync();

app.UseForwardedHeaders();
app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

app.MapGroup("/api")
    .MapQuotes()
    .MapReferrals()
    .MapPolicies()
    .MapDocuments()
    .MapCatalogue()
    .MapAgencies()
    .RequireAuthorization(Policies.AnyUser);

app.MapHub<QuoteHub>("/hubs/quotes");
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

public partial class Program;
