using System.Text.Json.Serialization;
using ExpenseClaims.Api.Authentication;
using ExpenseClaims.Api.Development;
using ExpenseClaims.Api.Services;
using ExpenseClaims.Core.Accounting;
using ExpenseClaims.Core.Policy;
using ExpenseClaims.Data;
using ExpenseClaims.Data.Setup;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, logger) => logger
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddExpenseClaimsDatabase(builder.Configuration);
    builder.Services.AddExpensesAuthentication(builder.Configuration);
    builder.Services.AddHttpContextAccessor();

    builder.Services.Configure<ExpensePolicyOptions>(builder.Configuration.GetSection(ExpensePolicyOptions.SectionName));
    builder.Services.Configure<ReceiptStoreOptions>(builder.Configuration.GetSection(ReceiptStoreOptions.SectionName));
    builder.Services.Configure<ApprovalOptions>(builder.Configuration.GetSection(ApprovalOptions.SectionName));
    builder.Services.Configure<AccountingOptions>(builder.Configuration.GetSection(AccountingOptions.SectionName));
    builder.Services.AddSingleton(sp => new VatRules(sp.GetRequiredService<IOptions<AccountingOptions>>().Value));
    builder.Services.AddSingleton(sp => new ExpensePolicy(sp.GetRequiredService<IOptions<ExpensePolicyOptions>>().Value));
    builder.Services.AddSingleton<IReceiptStore, FileSystemReceiptStore>();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();
    builder.Services.AddScoped<ClaimNumberAllocator>();
    builder.Services.AddScoped<ClaimService>();

    builder.Services.AddControllers()
        .AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            o.JsonSerializerOptions.Converters.Add(new DateOnlyJsonConverter());
        });
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();
    builder.Services.AddProblemDetailsForClaimRules();
    builder.Services.AddHealthChecks().AddDbContextCheck<ExpenseClaimsDbContext>("database");

    var spaOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
    builder.Services.AddCors(cors => cors.AddDefaultPolicy(p => p
        .WithOrigins(spaOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders("Content-Disposition")));

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "Expense Claims API", Version = "v1" });
        c.MapType<DateOnly>(() => new OpenApiSchema { Type = "string", Format = "date" });
        c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "ExpenseClaims.Api.xml"));
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
        });
    });

    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.KnownNetworks.Clear();
        o.KnownProxies.Clear();
    });

    var app = builder.Build();

    app.UseForwardedHeaders();
    app.UseSerilogRequestLogging();
    app.UseClaimRuleProblemDetails();

    if (app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health").AllowAnonymous();

    if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
        await app.Services.MigrateExpenseClaimsDatabaseAsync();
    if (app.Configuration.GetValue("Database:SeedTestData", false))
        await DevelopmentSeed.SeedAsync(app.Services);

    await app.RunAsync();
}
catch (Exception ex) when (ex.GetType().Name != "StopTheHostException")
{
    Log.Fatal(ex, "Expense Claims API terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program
{
}
