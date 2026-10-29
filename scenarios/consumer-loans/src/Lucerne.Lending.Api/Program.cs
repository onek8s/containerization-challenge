using Lucerne.Lending.Api.Infrastructure;
using Lucerne.Lending.Data;
using Lucerne.Lending.Workflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using NLog;
using NLog.Web;

var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    builder.Services.AddLendingWorkflow(builder.Configuration);
    builder.Services.AddAuthentication(ApiKeyHandler.SchemeName)
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ApiKeyHandler>(ApiKeyHandler.SchemeName, null);
    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        options.AddPolicy("Customer", p => p.RequireClaim("scope", ApiScopes.Portal, ApiScopes.Office, ApiScopes.Partner));
        options.AddPolicy("Office", p => p.RequireClaim("scope", ApiScopes.Office));
    });
    builder.Services.AddControllers(o => o.Filters.Add<DomainExceptionFilter>());
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "Lucerne Consumer Lending API", Version = "v1" });
        c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme { Name = "X-Api-Key", In = ParameterLocation.Header, Type = SecuritySchemeType.ApiKey });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" } }] = Array.Empty<string>()
        });
        c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Lucerne.Lending.Api.xml"));
    });
    if (builder.Configuration.GetValue("Lending:RunBackgroundWorkers", true))
    {
        builder.Services.AddHostedService<ScoringWorker>();
        builder.Services.AddHostedService<BookingWorker>();
    }

    var app = builder.Build();

    var dbOptions = app.Services.GetRequiredService<LendingDbOptions>();
    if (builder.Configuration.GetValue("Database:Upgrade", false))
    {
        SchemaUpgrader.Upgrade(dbOptions, app.Logger);
        await ReferenceData.SeedAsync(app.Services.GetRequiredService<ProductRepository>());
    }
    foreach (var client in builder.Configuration.GetSection("Seed:ApiClients").GetChildren())
        await app.Services.GetRequiredService<ApiClientRepository>().SetAsync(client.Key, client["Key"]!, client["Scopes"]!);

    if (app.Environment.IsDevelopment() || builder.Configuration.GetValue("Swagger:Enabled", false))
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapGet("/swagger", () => Results.Redirect("/swagger/index.html")).AllowAnonymous();
    app.Run();
}
catch (Exception ex)
{
    logger.Error(ex, "Lending API stopped because of an exception");
    throw;
}
finally
{
    LogManager.Shutdown();
}

public partial class Program { }
