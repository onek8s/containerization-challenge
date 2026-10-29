using System.Data.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CityPermits.Contracts;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));
var cfg = builder.Configuration;
var devMode = cfg["Authentication:Mode"] == "Development";
var devKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Authentication:DevelopmentSigningKey"] ?? "development-only-signing-key-0123456789"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    if (devMode)
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = "dev", ValidAudience = "inspections", IssuerSigningKey = devKey, NameClaimType = "upn", RoleClaimType = ClaimTypes.Role
        };
    }
    else
    {
        // ADFS 2019 application group "City Permits Inspections" (SPA client + Web API).
        o.Authority = cfg["Authentication:Authority"];
        o.Audience = cfg["Authentication:Audience"];
        o.TokenValidationParameters.ValidIssuer = cfg["Authentication:Issuer"];
        o.TokenValidationParameters.NameClaimType = "upn";
        o.TokenValidationParameters.RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
    }
});
builder.Services.AddAuthorization(o => o.AddPolicy("Inspector", p => p.RequireRole("Inspector", "Supervisor")));
builder.Services.AddSingleton<Db>();
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o => o.SerializerOptions.Converters.Add(new DateOnlyConverter()));

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/config", () => Results.Ok(new { devMode, authority = cfg["Authentication:Authority"], clientId = cfg["Authentication:ClientId"], mapTiles = cfg["MapTiles"] }));

if (devMode)
{
    app.MapPost("/api/dev-token", (DevLogin login) =>
    {
        var token = new JwtSecurityToken("dev", "inspections",
            new[] { new Claim("upn", login.User), new Claim(ClaimTypes.Role, "Inspector") },
            expires: DateTime.UtcNow.AddHours(10), signingCredentials: new SigningCredentials(devKey, SecurityAlgorithms.HmacSha256));
        return Results.Ok(new { access_token = new JwtSecurityTokenHandler().WriteToken(token) });
    });
}

app.MapGet("/api/visits", async (DateTime? date, ClaimsPrincipal user, Db db) =>
{
    var day = (date ?? DateTime.Today).Date;
    var who = user.Identity!.Name!;
    using var c = db.Open();
    var rows = await c.QueryAsync<VisitRow>(db.Sql(@"
SELECT i.Id, i.CaseId, c.Reference AS CaseReference, i.Stage, i.Status, i.ScheduledDate, i.Slot, i.RouteOrder, c.SiteAddress, c.Easting, c.Northing,
       a.DisplayName AS ApplicantName, a.Phone AS ApplicantPhone, c.Title, i.ChecklistJson, i.ResultNotes
FROM {Inspections} i JOIN {Cases} c ON c.Id = i.CaseId JOIN {Applicants} a ON a.Id = c.ApplicantId
WHERE i.Inspector = @who AND i.ScheduledDate = @day ORDER BY i.RouteOrder, i.Slot, i.Id"), new { who, day = db.DateParam(day) });
    return Results.Ok(rows.Select(r => r.ToDto()));
}).RequireAuthorization("Inspector");

// Results are posted from the device queue; ClientMutationId makes retries after a lost response safe.
app.MapPost("/api/results", async (InspectionResultDto result, ClaimsPrincipal user, Db db) =>
{
    using var c = db.Open();
    var existing = await c.QuerySingleOrDefaultAsync<(int Id, int Status)?>(db.Sql("SELECT Id, Status FROM {Inspections} WHERE ClientMutationId = @m"), new { m = result.ClientMutationId });
    if (existing is { } e) return Results.Ok(new InspectionResultAck(result.ClientMutationId, e.Id, ((Status)e.Status).ToString(), true));

    var visit = await c.QuerySingleOrDefaultAsync<(string Stage, string Inspector)?>(db.Sql("SELECT Stage, Inspector FROM {Inspections} WHERE Id = @id"), new { id = result.InspectionId });
    if (visit is null) return Results.NotFound();
    if (!string.Equals(visit.Value.Inspector, user.Identity!.Name, StringComparison.OrdinalIgnoreCase)) return Results.Forbid();

    var status = result.NoAccess ? Status.NoAccess
        : result.Answers.Any(a => a.Passed == false && Mandatory(visit.Value.Stage, a.Code)) ? Status.Failed
        : result.Answers.Any(a => a.Passed == false) ? Status.PassedWithRemarks : Status.Passed;
    await c.ExecuteAsync(db.Sql(@"UPDATE {Inspections} SET Status = @status, StartedUtc = @s, CompletedUtc = @f, ResultNotes = @notes,
        ChecklistJson = @json, Latitude = @lat, Longitude = @lon, ClientMutationId = @m WHERE Id = @id"),
        new { status = (int)status, s = result.StartedUtc, f = result.CompletedUtc, notes = result.Notes, json = JsonSerializer.Serialize(result.Answers),
              lat = result.Latitude, lon = result.Longitude, m = result.ClientMutationId, id = result.InspectionId });
    await c.ExecuteAsync(db.Sql(@"INSERT INTO {CaseEvents} (CaseId, AtUtc, Actor, Type, Text, VisibleToApplicant)
        SELECT CaseId, @now, @who, 'inspection', @text, @visible FROM {Inspections} WHERE Id = @id"),
        new { now = DateTime.UtcNow, who = user.Identity!.Name, text = $"{visit.Value.Stage}: {status}", visible = true, id = result.InspectionId });
    return Results.Ok(new InspectionResultAck(result.ClientMutationId, result.InspectionId, status.ToString(), false));
}).RequireAuthorization("Inspector");

app.MapFallbackToFile("index.html");
app.Run();

static bool Mandatory(string stage, string code) => !code.EndsWith("-4") || stage is "Food hygiene" or "Completion";

record DevLogin(string User);

enum Status { Requested = 0, Scheduled = 1, InProgress = 2, Passed = 3, PassedWithRemarks = 4, Failed = 5, Cancelled = 6, NoAccess = 7 }

class VisitRow
{
    public int Id { get; set; } public int CaseId { get; set; } public string CaseReference { get; set; } = ""; public string Stage { get; set; } = "";
    public int Status { get; set; } public DateTime? ScheduledDate { get; set; } public string? Slot { get; set; } public int? RouteOrder { get; set; }
    public string? SiteAddress { get; set; } public double? Easting { get; set; } public double? Northing { get; set; }
    public string ApplicantName { get; set; } = ""; public string? ApplicantPhone { get; set; } public string Title { get; set; } = "";
    public string? ChecklistJson { get; set; } public string? ResultNotes { get; set; }

    public InspectionVisitDto ToDto() => new(Id, CaseId, CaseReference, Stage, ((Status)Status).ToString(),
        ScheduledDate is null ? null : DateOnly.FromDateTime(ScheduledDate.Value), Slot, RouteOrder, SiteAddress, null, null,
        ApplicantName, ApplicantPhone, Title, Checklists.For(Stage, ChecklistJson), ResultNotes);
}

static class Checklists
{
    // Kept in step with CityPermits.Domain.Inspections.InspectionStages (this app runs on its own release train).
    static readonly Dictionary<string, (string, string, bool)[]> Items = new()
    {
        ["Foundations"] = new[] { ("FDN-1", "Trench depth matches approved drawings", true), ("FDN-2", "Bearing strata suitable, no made ground", true), ("FDN-3", "Trees within influence distance considered", false) },
        ["Drainage"] = new[] { ("DRN-1", "Air test passed", true), ("DRN-2", "Falls adequate, access points provided", true), ("DRN-3", "Connection to correct sewer", true) },
        ["Completion"] = new[] { ("CMP-1", "Smoke alarms interlinked and working", true), ("CMP-2", "Escape windows provided where required", true), ("CMP-3", "Electrical certificate received", true), ("CMP-4", "Energy performance certificate received", false) },
        ["Food hygiene"] = new[] { ("FHY-1", "Hand washing facilities with hot water", true), ("FHY-2", "Fridge temperatures below 8 °C", true), ("FHY-3", "Food safety management system", true), ("FHY-4", "Pest control contract", false) },
        ["Fire safety"] = new[] { ("FIR-1", "Fire risk assessment available", true), ("FIR-2", "Escape routes clear and signed", true), ("FIR-3", "Emergency lighting tested", true) },
    };

    public static IReadOnlyList<ChecklistItemDto> For(string stage, string? json)
    {
        var answers = json is null ? new List<ChecklistAnswerDto>() : JsonSerializer.Deserialize<List<ChecklistAnswerDto>>(json) ?? new();
        var items = Items.TryGetValue(stage, out var i) ? i : new[] { ("GEN-1", "Premises as described in the application", true) };
        return items.Select(x => { var a = answers.FirstOrDefault(y => y.Code == x.Item1); return new ChecklistItemDto(x.Item1, x.Item2, x.Item3, a?.Passed, a?.Note); }).ToList();
    }
}

class Db
{
    private readonly string _provider;
    private readonly string _cs;

    public Db(IConfiguration cfg) { _provider = cfg["Database:Provider"] ?? "SqlServer"; _cs = cfg.GetConnectionString("Permits")!; }

    public bool IsSqlite => _provider == "Sqlite";

    public DbConnection Open()
    {
        DbConnection c = IsSqlite ? new Microsoft.Data.Sqlite.SqliteConnection(_cs) : new Microsoft.Data.SqlClient.SqlConnection(_cs);
        c.Open();
        return c;
    }

    public string Sql(string sql) => sql.Replace("{", IsSqlite ? "" : "permits.").Replace("}", "");

    public object DateParam(DateTime d) => IsSqlite ? d.ToString("yyyy-MM-dd") : d;
}

class DateOnlyConverter : System.Text.Json.Serialization.JsonConverter<DateOnly>
{
    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => DateOnly.Parse(reader.GetString()!);
    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString("yyyy-MM-dd"));
}
