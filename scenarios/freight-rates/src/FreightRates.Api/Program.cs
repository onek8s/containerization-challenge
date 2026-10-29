using FreightRates.Api.Endpoints;
using FreightRates.Api.Infrastructure;
using FreightRates.Contracts;
using FreightRates.Data;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFreightRatesData(builder.Configuration);

builder.Services.AddRatingHostClient(builder.Configuration);

builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorization();

builder.Services.AddExceptionHandler<RatingExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<FreightRatesContext>("database")
    .AddCheck<RatingHostHealthCheck>("rating-host");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "Contoso Freight Rates API", Version = "v1" });
    var scheme = new OpenApiSecurityScheme
    {
        Name = ApiKeyAuthenticationHandler.HeaderName,
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = ApiKeyAuthenticationHandler.SchemeName },
    };
    o.AddSecurityDefinition(ApiKeyAuthenticationHandler.SchemeName, scheme);
    o.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FreightRatesContext>();
    await DbInitializer.InitializeAsync(db, app.Configuration.GetValue<bool>("Database:SeedSampleData"));
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapQuoteEndpoints();

app.Run();

public partial class Program;
