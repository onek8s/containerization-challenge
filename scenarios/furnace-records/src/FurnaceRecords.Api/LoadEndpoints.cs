using System.Text;
using FurnaceRecords.Core;
using FurnaceRecords.Data;
using FurnaceRecords.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FurnaceRecords.Api;

public sealed record PlanLoadDto(string Furnace, string Recipe, List<PlannedPart>? Parts, LoadKind Kind = LoadKind.Production);

public sealed record LoadDto(
    string LoadNumber,
    string Kind,
    string Furnace,
    string Recipe,
    string Status,
    DateTime PlannedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? EndedAtUtc,
    bool? Passed,
    double? SoakMinutes,
    double? QuenchDelaySeconds,
    double? UniformityCelsius,
    string? DecidedBy,
    DateTime? DecidedAtUtc,
    IReadOnlyList<PlannedPart> Parts,
    IReadOnlyList<Finding> Findings);

public static class LoadEndpoints
{
    public static void MapLoadEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization();

        api.MapGet("/furnaces", async (FurnaceRecordsContext db, CancellationToken ct) =>
            await db.Furnaces.AsNoTracking().Where(f => f.Active).OrderBy(f => f.Code)
                .Select(f => new { f.Code, f.Name, f.FurnaceClass, Instrumentation = f.Instrumentation.ToString(), f.UniformitySurveyDue, f.AccuracyTestDue })
                .ToListAsync(ct));

        api.MapGet("/recipes", async (FurnaceRecordsContext db, CancellationToken ct) =>
            await db.Recipes.AsNoTracking().Where(r => r.Active).OrderBy(r => r.Code)
                .Select(r => new { r.Code, r.Name, r.Specification, r.SetpointCelsius, r.ToleranceMinusCelsius, r.TolerancePlusCelsius, r.SoakMinutes })
                .ToListAsync(ct));

        api.MapPost("/loads", async (PlanLoadDto dto, LoadService loads, HttpContext http, CancellationToken ct) =>
        {
            try
            {
                var load = await loads.PlanAsync(new PlanLoadRequest(dto.Furnace, dto.Recipe, dto.Parts ?? new(), http.User.Identity!.Name!, dto.Kind), ct);
                var created = await loads.FindAsync(load.LoadNumber, ct);
                return Results.Created($"/api/v1/loads/{load.LoadNumber}", ToDto(created!));
            }
            catch (LoadException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
            }
        });

        api.MapGet("/loads", async (string? furnace, string? status, LoadService loads, CancellationToken ct) =>
        {
            LoadStatus? s = Enum.TryParse<LoadStatus>(status, true, out var parsed) ? parsed : null;
            var list = await loads.ListAsync(furnace, s, 200, ct);
            return list.Select(l => new { l.LoadNumber, Furnace = l.Furnace!.Code, Recipe = l.Recipe!.Code, Status = l.Status.ToString(), l.PlannedAtUtc, l.EndedAtUtc, l.Passed });
        });

        api.MapGet("/loads/{loadNumber}", async (string loadNumber, LoadService loads, CancellationToken ct) =>
            await loads.FindAsync(loadNumber, ct) is { } load ? Results.Ok(ToDto(load)) : Results.NotFound());

        api.MapGet("/loads/{loadNumber}/samples.csv", async (string loadNumber, LoadService loads, IConfiguration config, CancellationToken ct) =>
        {
            var load = await loads.FindAsync(loadNumber, ct);
            if (load is null) return Results.NotFound();
            var zone = TimeZoneInfo.FindSystemTimeZoneById(config["Plant:TimeZone"] ?? "UTC");
            var names = load.Furnace!.Channels.Where(c => c.Enabled).ToDictionary(c => c.Channel, c => c.Name);
            var csv = SampleCsv.Write(await loads.SamplesAsync(load.Id, ct), names, zone);
            return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv", $"{loadNumber}.csv");
        });

        api.MapGet("/loads/{loadNumber}/certificate", async (string loadNumber, LoadService loads, CancellationToken ct) =>
        {
            var load = await loads.FindAsync(loadNumber, ct);
            if (load is null) return Results.NotFound();
            if (load.Status != LoadStatus.Released)
            {
                return Results.Problem($"Load {loadNumber} is {load.Status}; certificates are issued for released loads only.", statusCode: StatusCodes.Status409Conflict);
            }
            var r = load.Recipe!;
            return Results.Ok(new
            {
                Certificate = $"HT-{load.LoadNumber}",
                load.LoadNumber,
                Furnace = new { load.Furnace!.Code, load.Furnace.Name, load.Furnace.FurnaceClass, Instrumentation = load.Furnace.Instrumentation.ToString() },
                Recipe = new { r.Code, r.Name, r.Specification, r.SetpointCelsius, r.ToleranceMinusCelsius, r.TolerancePlusCelsius, r.SoakMinutes },
                load.StartedAtUtc,
                load.EndedAtUtc,
                load.SoakStartUtc,
                load.SoakEndUtc,
                load.SoakMinutes,
                load.QuenchDelaySeconds,
                Parts = load.Parts.Select(p => new PlannedPart(p.WorkOrder, p.PartNumber, p.Lot, p.Quantity)),
                ReleasedBy = load.DecidedBy,
                ReleasedAtUtc = load.DecidedAtUtc,
                load.DecisionComment,
            });
        });
    }

    private static LoadDto ToDto(Load l) => new(
        l.LoadNumber, l.Kind.ToString(), l.Furnace!.Code, l.Recipe!.Code, l.Status.ToString(), l.PlannedAtUtc, l.StartedAtUtc, l.EndedAtUtc,
        l.Passed, l.SoakMinutes, l.QuenchDelaySeconds, l.UniformityCelsius, l.DecidedBy, l.DecidedAtUtc,
        l.Parts.Select(p => new PlannedPart(p.WorkOrder, p.PartNumber, p.Lot, p.Quantity)).ToList(),
        LoadService.Findings(l));
}
