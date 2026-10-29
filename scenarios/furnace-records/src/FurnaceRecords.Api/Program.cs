using System.Text.Json.Serialization;
using FurnaceRecords.Api;
using FurnaceRecords.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFurnaceRecordsData(builder.Configuration);
builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "Furnace Records MES API", Version = "v1" });
    o.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme { Name = ApiKeyAuthenticationHandler.Header, In = ParameterLocation.Header, Type = SecuritySchemeType.ApiKey });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" } }] = Array.Empty<string>(),
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await DbInitializer.InitializeAsync(scope.ServiceProvider.GetRequiredService<FurnaceRecordsContext>(),
        scope.ServiceProvider.GetRequiredService<DatabaseOptions>());

    // FurnaceRecords.Api.exe issue-key <client name>
    if (args.Length == 2 && args[0] == "issue-key")
    {
        var key = await scope.ServiceProvider.GetRequiredService<ApiKeyService>().IssueAsync(args[1]);
        Console.WriteLine($"API key for {args[1]}: {key}");
        return;
    }
}

app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

app.MapLoadEndpoints();
app.MapGet("/health", async (FurnaceRecordsContext db) =>
    await db.Database.CanConnectAsync() ? Results.Ok(new { status = "Healthy" }) : Results.Json(new { status = "Unhealthy" }, statusCode: 503));

app.Run();

public partial class Program
{
}
