using Microsoft.EntityFrameworkCore;
using PartsCatalog.Data;
using PartsCatalog.Models;
using PartsCatalog.Services;

var builder = WebApplication.CreateBuilder(args);

var logFolder = builder.Configuration["Catalog:LogFolder"] ?? "logs";
builder.Logging.AddProvider(new FileLoggerProvider(Path.Combine(builder.Environment.ContentRootPath, logFolder)));

builder.Services.AddDbContext<CatalogContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Catalog")));
builder.Services.AddScoped<PartService>();
builder.Services.AddRazorPages();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogContext>();
    var dataFolder = builder.Configuration["Catalog:DataFolder"];
    if (!string.IsNullOrEmpty(dataFolder))
    {
        Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, dataFolder));
    }

    SeedData.Initialize(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

var api = app.MapGroup("/api/parts");

api.MapGet("/", async (PartService svc, string? q, string? category, bool? reorder) =>
    Results.Ok(await svc.SearchAsync(q, category, reorder ?? false)));

api.MapGet("/{partNumber}", async (PartService svc, string partNumber) =>
    await svc.FindByNumberAsync(partNumber.ToUpperInvariant()) is Part p ? Results.Ok(p) : Results.NotFound());

api.MapPost("/{partNumber}/adjust", async (PartService svc, ILogger<Program> log, string partNumber, StockAdjustment body) =>
{
    try
    {
        var part = await svc.AdjustStockAsync(partNumber.ToUpperInvariant(), body.Delta);
        if (part == null)
        {
            return Results.NotFound();
        }

        log.LogInformation("Stock adjusted for {PartNumber} by {Delta} ({Reason}); now {Qty}", part.PartNumber, body.Delta, body.Reason ?? "n/a", part.QuantityOnHand);
        return Results.Ok(part);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

app.MapGet("/ping", () => Results.Text("OK"));

app.Run();

public record StockAdjustment(int Delta, string? Reason);

public partial class Program
{
}
