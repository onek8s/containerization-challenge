using System.Globalization;
using System.Text;
using System.Text.Json;
using AspNetCoreRateLimit;
using CityPermits.Contracts;
using CityPermits.Data;
using CityPermits.Data.Reporting;
using CityPermits.Domain.Model;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));
builder.Services.AddPermitsData(builder.Configuration);
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "City of Ashford permits open data", Version = "v1" }));
builder.Services.AddResponseCaching();

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseIpRateLimiting();
app.UseResponseCaching();
app.UseSwagger();
app.UseSwaggerUI();

var licence = "Contains public sector information licensed under the Open Government Licence v3.0.";

IQueryable<PermitCase> Published(PermitsDbContext db) => db.Cases.AsNoTracking()
    .Where(c => c.Status == CaseStatus.Issued || c.Status == CaseStatus.Refused || c.Status == CaseStatus.Expired);

IssuedPermitDto ToDto(PermitCase c)
{
    double? lat = null, lon = null;
    if (c.Easting is { } e && c.Northing is { } n)
    {
        // Rounded to ~10 m; parking permits are published by zone only (privacy).
        (lat, lon) = Osgb.ToLatLon(Math.Round(e / 10) * 10, Math.Round(n / 10) * 10);
    }
    var parking = c.Kind == PermitKind.ParkingPermit;
    return new IssuedPermitDto(c.Reference, c.Kind.ToString(), parking ? $"Parking permit zone {c.Parking?.ZoneCode}" : c.Title, c.Ward,
        parking ? null : c.SiteAddress, parking ? null : lat, parking ? null : lon, c.DecidedUtc, c.Outcome?.ToString(), c.ValidFrom, c.ValidTo,
        c.Building?.WorkType.ToString(), c.Business?.Category.ToString(), c.Business?.TradingName);
}

app.MapGet("/v1/permits", async (PermitsDbContext db, string? kind, string? ward, DateTime? decidedFrom, int page = 1, int pageSize = 100) =>
{
    pageSize = Math.Clamp(pageSize, 1, 500);
    var q = Published(db);
    if (Enum.TryParse<PermitKind>(kind, true, out var k)) q = q.Where(c => c.Kind == k);
    if (!string.IsNullOrEmpty(ward)) q = q.Where(c => c.Ward == ward);
    if (decidedFrom is not null) q = q.Where(c => c.DecidedUtc >= decidedFrom);
    var total = await q.CountAsync();
    var items = await q.OrderByDescending(c => c.DecidedUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
    return Results.Ok(new PagedResult<IssuedPermitDto>(items.Select(ToDto).ToList(), page, pageSize, total));
}).WithName("ListPermits").CacheOutputHeaders();

app.MapGet("/v1/permits/{reference}", async (string reference, PermitsDbContext db) =>
    await Published(db).FirstOrDefaultAsync(c => c.Reference == reference.Replace('-', '/')) is { } c ? Results.Ok(ToDto(c)) : Results.NotFound());

app.MapGet("/v1/permits.csv", async (PermitsDbContext db, HttpContext ctx) =>
{
    var rows = (await Published(db).OrderBy(c => c.Reference).ToListAsync()).Select(ToDto);
    ctx.Response.ContentType = "text/csv; charset=utf-8";
    ctx.Response.Headers.ContentDisposition = "attachment; filename=ashford-permits.csv";
    await using var w = new StreamWriter(ctx.Response.Body, new UTF8Encoding(true));
    await using var csv = new CsvWriter(w, CultureInfo.InvariantCulture);
    await csv.WriteRecordsAsync(rows);
});

app.MapGet("/v1/permits.geojson", async (PermitsDbContext db, string? kind) =>
{
    var q = Published(db).Where(c => c.Kind != PermitKind.ParkingPermit && c.Easting != null);
    if (Enum.TryParse<PermitKind>(kind, true, out var k)) q = q.Where(c => c.Kind == k);
    var features = (await q.ToListAsync()).Select(ToDto).Select(d => new
    {
        type = "Feature",
        geometry = new { type = "Point", coordinates = new[] { d.Longitude, d.Latitude } },
        properties = d
    });
    return Results.Text(JsonSerializer.Serialize(new { type = "FeatureCollection", licence, features }, new JsonSerializerOptions(JsonSerializerDefaults.Web)), "application/geo+json");
});

app.MapGet("/v1/statistics", async (ReportQueries reports, int? year) =>
{
    var y = year ?? DateTime.UtcNow.Year;
    return Results.Ok(await reports.PerformanceAsync(new DateTime(y, 1, 1), new DateTime(y + 1, 1, 1)));
});

app.MapGet("/health", () => Results.Ok("ok"));
app.Run();

static class EndpointExtensions
{
    public static RouteHandlerBuilder CacheOutputHeaders(this RouteHandlerBuilder b) =>
        b.AddEndpointFilter(async (ctx, next) =>
        {
            ctx.HttpContext.Response.Headers.CacheControl = "public, max-age=300";
            return await next(ctx);
        });
}

/// <summary>OSGB36 National Grid to WGS84 (Helmert, ~5 m accuracy; good enough for open data points).</summary>
static class Osgb
{
    public static (double Lat, double Lon) ToLatLon(double E, double N)
    {
        double a = 6377563.396, b = 6356256.909, F0 = 0.9996012717, lat0 = 49 * Math.PI / 180, lon0 = -2 * Math.PI / 180, N0 = -100000, E0 = 400000;
        double e2 = 1 - b * b / (a * a), n = (a - b) / (a + b);
        double lat = lat0, M = 0;
        do
        {
            lat = (N - N0 - M) / (a * F0) + lat;
            double Ma = (1 + n + 5.0 / 4 * n * n + 5.0 / 4 * n * n * n) * (lat - lat0);
            double Mb = (3 * n + 3 * n * n + 21.0 / 8 * n * n * n) * Math.Sin(lat - lat0) * Math.Cos(lat + lat0);
            double Mc = (15.0 / 8 * n * n + 15.0 / 8 * n * n * n) * Math.Sin(2 * (lat - lat0)) * Math.Cos(2 * (lat + lat0));
            double Md = 35.0 / 24 * n * n * n * Math.Sin(3 * (lat - lat0)) * Math.Cos(3 * (lat + lat0));
            M = b * F0 * (Ma - Mb + Mc - Md);
        } while (N - N0 - M >= 0.00001);
        double sin = Math.Sin(lat), cos = Math.Cos(lat), tan = Math.Tan(lat);
        double nu = a * F0 / Math.Sqrt(1 - e2 * sin * sin), rho = a * F0 * (1 - e2) / Math.Pow(1 - e2 * sin * sin, 1.5), eta2 = nu / rho - 1;
        double VII = tan / (2 * rho * nu), VIII = tan / (24 * rho * nu * nu * nu) * (5 + 3 * tan * tan + eta2 - 9 * tan * tan * eta2);
        double X = 1 / (cos * nu), XI = 1 / (cos * 6 * nu * nu * nu) * (nu / rho + 2 * tan * tan);
        double dE = E - E0;
        double phi = lat - VII * dE * dE + VIII * Math.Pow(dE, 4);
        double lambda = lon0 + X * dE - XI * dE * dE * dE;
        // OSGB36 -> WGS84 approximate shift for south east England
        return (phi * 180 / Math.PI + 0.00045, lambda * 180 / Math.PI - 0.00155);
    }
}
