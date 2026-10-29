using System.Security.Cryptography;
using System.Text;
using CityPermits.Contracts;
using CityPermits.Gis;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));
builder.Services.AddCityGis(builder.Configuration);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>())
    .WithMethods("GET").AllowAnyHeader()));

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseCors();

// Calling applications send X-Map-Key; tiles are public (used by Leaflet in browsers).
var keys = (app.Configuration.GetSection("MapKeys").Get<string[]>() ?? Array.Empty<string>()).ToHashSet();
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    if (path.StartsWith("/tiles/") || path == "/health")
    {
        await next();
        return;
    }
    var key = ctx.Request.Headers["X-Map-Key"].ToString();
    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
    if (string.IsNullOrEmpty(key) || !keys.Contains(hash))
    {
        ctx.Response.StatusCode = 401;
        return;
    }
    await next();
});

// The engine is created on first use; warm it up so the first user does not wait for licence checkout.
app.Lifetime.ApplicationStarted.Register(() => Task.Run(() => app.Services.GetRequiredService<IGisService>().Health()));

app.MapGet("/health", (IGisService gis) => gis.Health() is { Ready: true } h ? Results.Ok(h) : Results.Json(gis.Health(), statusCode: 503));

app.MapGet("/tiles/{map}/{z:int}/{x:int}/{y:int}.png", (string map, int z, int x, int y, IGisService gis, HttpContext ctx) =>
{
    if (z is < 12 or > 20) return Results.NotFound();
    var img = gis.RenderTile(map, z, x, y);
    ctx.Response.Headers.CacheControl = "public, max-age=3600";
    return Results.File(img.Content, img.ContentType);
});

app.MapGet("/parcels/{id}", (string id, IGisService gis) => gis.GetParcel(id) is { } p ? Results.Ok(p) : Results.NotFound());
app.MapGet("/parcels", (string? q, double? e, double? n, double? lat, double? lon, IGisService gis) =>
{
    if (lat is not null && lon is not null) (e, n) = Coordinates.FromLatLon(lat.Value, lon.Value);
    if (e is not null && n is not null) return gis.FindParcelAt(e.Value, n.Value) is { } p ? Results.Ok(new[] { p }) : Results.Ok(Array.Empty<ParcelDto>());
    return string.IsNullOrWhiteSpace(q) ? Results.BadRequest() : Results.Ok(gis.SearchParcels(q));
});
app.MapPost("/site-checks", (SiteCheckRequest req, IGisService gis) =>
{
    try { return Results.Ok(gis.CheckSite(req)); }
    catch (KeyNotFoundException ex) { return Results.NotFound(ex.Message); }
});
app.MapGet("/identify", (double lat, double lon, double? tolerance, string? layers, IGisService gis) =>
{
    var (e, n) = Coordinates.FromLatLon(lat, lon);
    return Results.Ok(gis.Identify(e, n, tolerance ?? 5, layers?.Split(',')));
});
app.MapPost("/exports", (MapExportRequest req, IGisService gis) =>
{
    var img = gis.ExportMap(req);
    return Results.File(img.Content, img.ContentType, $"{req.Template}-{req.ParcelId}.{req.Format}");
});

app.Run();
