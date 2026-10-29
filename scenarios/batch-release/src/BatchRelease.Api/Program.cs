using System.Text.Json.Serialization;
using BatchRelease.Api;
using BatchRelease.Contracts;
using BatchRelease.Data;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBatchReleaseData(builder.Configuration);
builder.Services.AddSigningHostClient(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Submit", p => p.RequireClaim(ApiKeyAuthenticationHandler.SubmitClaim))
    .AddPolicy("Read", p => p.RequireClaim(ApiKeyAuthenticationHandler.ReadClaim));

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddDbContextCheck<BatchReleaseContext>("database");

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapBatchEndpoints();

app.Run();

public partial class Program;
