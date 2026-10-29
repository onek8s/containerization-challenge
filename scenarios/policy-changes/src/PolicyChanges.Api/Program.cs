using System.Text.Json.Serialization;
using PolicyChanges.Api;
using PolicyChanges.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPolicyChangesData(builder.Configuration);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHealthChecks().AddDbContextCheck<PolicyChangesContext>("database");
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "Tailspin Policy Changes", Version = "v1" });
    o.AddSecurityDefinition("ApiKey", new()
    {
        Name = PartnerAuthentication.Header,
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
    });
    o.AddSecurityRequirement(new()
    {
        [new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "ApiKey" } }] = Array.Empty<string>(),
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PolicyChangesContext>();
    await DbInitializer.InitializeAsync(db, app.Configuration.GetValue<bool>("Database:SeedSampleData"));
}

app.UseSwagger();
app.UseSwaggerUI();
app.MapHealthChecks("/health");
app.MapChangeEndpoints();

app.Run();

public partial class Program;
