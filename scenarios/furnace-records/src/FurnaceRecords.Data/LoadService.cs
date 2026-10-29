using System.Text.Json;
using FurnaceRecords.Core;
using FurnaceRecords.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FurnaceRecords.Data;

public sealed record PlannedPart(string WorkOrder, string PartNumber, string? Lot, int Quantity);

public sealed record PlanLoadRequest(string FurnaceCode, string RecipeCode, IReadOnlyList<PlannedPart> Parts, string PlannedBy, LoadKind Kind = LoadKind.Production);

public sealed class LoadException : Exception
{
    public LoadException(string message) : base(message)
    {
    }
}

public sealed class LoadService
{
    public static readonly TimeSpan DefaultRecordInterval = TimeSpan.FromSeconds(10);

    private readonly FurnaceRecordsContext _db;
    private readonly TimeProvider _time;

    public LoadService(FurnaceRecordsContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<Load> PlanAsync(PlanLoadRequest request, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var furnace = await _db.Furnaces.Include(f => f.Channels).FirstOrDefaultAsync(f => f.Code == request.FurnaceCode && f.Active, ct);
        var recipe = await _db.Recipes.FirstOrDefaultAsync(r => r.Code == request.RecipeCode && r.Active, ct);
        if (furnace is null) errors.Add($"Furnace {request.FurnaceCode} not found.");
        if (recipe is null) errors.Add($"Recipe {request.RecipeCode} not found.");
        var survey = request.Kind == LoadKind.Survey;
        if (request.Parts.Count == 0 && !survey) errors.Add("At least one part is required.");
        foreach (var p in request.Parts)
        {
            if (string.IsNullOrWhiteSpace(p.WorkOrder) || string.IsNullOrWhiteSpace(p.PartNumber)) errors.Add("Work order and part number are required.");
            if (p.Quantity <= 0) errors.Add($"Quantity of {p.PartNumber} must be positive.");
        }
        if (survey && furnace is not null)
        {
            var sensors = furnace.Channels.Count(c => c.Enabled && c.Role == ChannelRole.Survey);
            if (sensors < SurveyEvaluator.MinimumSensors)
            {
                errors.Add($"Furnace {furnace.Code} has {sensors} survey sensors connected; {SurveyEvaluator.MinimumSensors} are required.");
            }
        }
        if (furnace is not null && recipe is not null && !survey)
        {
            var tolerance = Math.Min(recipe.ToleranceMinusCelsius, recipe.TolerancePlusCelsius);
            if (!FurnaceClass.CanRun(furnace.FurnaceClass, tolerance))
            {
                errors.Add($"Furnace {furnace.Code} (class {furnace.FurnaceClass}) is not qualified for recipe {recipe.Code}.");
            }
            var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
            if (furnace.UniformitySurveyDue < today) errors.Add($"Uniformity survey of furnace {furnace.Code} is overdue.");
            if (furnace.AccuracyTestDue < today) errors.Add($"System accuracy test of furnace {furnace.Code} is overdue.");
        }
        if (errors.Count > 0)
        {
            throw new LoadException(string.Join(" ", errors.Distinct()));
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var prefix = LoadNumbers.Format(furnace!.Code, now.Year, 0)[..^5];
        var count = await _db.Loads.CountAsync(l => l.LoadNumber.StartsWith(prefix), ct);
        var load = new Load
        {
            LoadNumber = LoadNumbers.Format(furnace.Code, now.Year, count + 1),
            Kind = request.Kind,
            FurnaceId = furnace.Id,
            RecipeId = recipe!.Id,
            Status = LoadStatus.Planned,
            PlannedAtUtc = now,
            PlannedBy = request.PlannedBy,
            Parts = request.Parts.Select(p => new LoadPart { WorkOrder = p.WorkOrder.Trim(), PartNumber = p.PartNumber.Trim(), Lot = p.Lot?.Trim(), Quantity = p.Quantity }).ToList(),
        };
        _db.Loads.Add(load);
        await _db.SaveChangesAsync(ct);
        return load;
    }

    public Task<Load?> FindAsync(string loadNumber, CancellationToken ct = default) =>
        _db.Loads
            .Include(l => l.Furnace).ThenInclude(f => f!.Channels)
            .Include(l => l.Recipe)
            .Include(l => l.Parts)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.LoadNumber == loadNumber, ct);

    public Task<List<Load>> ListAsync(string? furnaceCode, LoadStatus? status, int take, CancellationToken ct = default)
    {
        var q = _db.Loads.AsNoTracking().Include(l => l.Furnace).Include(l => l.Recipe).AsQueryable();
        if (!string.IsNullOrEmpty(furnaceCode)) q = q.Where(l => l.Furnace!.Code == furnaceCode);
        if (status is not null) q = q.Where(l => l.Status == status);
        return q.OrderByDescending(l => l.PlannedAtUtc).Take(take).ToListAsync(ct);
    }

    public Task<Load?> NextPlannedAsync(int furnaceId, CancellationToken ct = default) =>
        _db.Loads.Include(l => l.Recipe)
            .Where(l => l.FurnaceId == furnaceId && l.Status == LoadStatus.Planned)
            .OrderBy(l => l.PlannedAtUtc)
            .FirstOrDefaultAsync(ct);

    public async Task<Load> StartAsync(Load load, DateTime atUtc, CancellationToken ct = default)
    {
        load.Status = LoadStatus.Running;
        load.StartedAtUtc = atUtc;
        _db.Events.Add(new LoadEventRecord { LoadId = load.Id, AtUtc = atUtc, Kind = LoadEventKind.CycleStart, Detail = load.Recipe?.Code });
        await _db.SaveChangesAsync(ct);
        return load;
    }

    public async Task AppendAsync(int loadId, IReadOnlyList<Sample> samples, IReadOnlyList<LoadEvent> events, CancellationToken ct = default)
    {
        _db.Samples.AddRange(samples.Select(s => new LoadSample { LoadId = loadId, AtUtc = s.AtUtc, Channel = s.Channel, Celsius = s.Celsius }));
        _db.Events.AddRange(events.Select(e => new LoadEventRecord { LoadId = loadId, AtUtc = e.AtUtc, Kind = e.Kind, Detail = e.Detail }));
        await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.Clear();
    }

    public async Task<IReadOnlyList<Sample>> SamplesAsync(int loadId, CancellationToken ct = default) =>
        await _db.Samples.AsNoTracking().Where(s => s.LoadId == loadId)
            .OrderBy(s => s.AtUtc).ThenBy(s => s.Channel)
            .Select(s => new Sample(s.AtUtc, s.Channel, s.Celsius))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LoadEvent>> EventsAsync(int loadId, CancellationToken ct = default) =>
        await _db.Events.AsNoTracking().Where(e => e.LoadId == loadId)
            .OrderBy(e => e.AtUtc)
            .Select(e => new LoadEvent(e.AtUtc, e.Kind, e.Detail))
            .ToListAsync(ct);

    /// <summary>Ends the load and stores the evaluation against its recipe.</summary>
    public async Task<LoadEvaluation> CompleteAsync(int loadId, DateTime endedAtUtc, TimeSpan recordInterval, CancellationToken ct = default)
    {
        var load = await _db.Loads.Include(l => l.Recipe).Include(l => l.Parts).Include(l => l.Furnace).ThenInclude(f => f!.Channels)
            .AsSplitQuery().FirstAsync(l => l.Id == loadId, ct);
        _db.Events.Add(new LoadEventRecord { LoadId = loadId, AtUtc = endedAtUtc, Kind = LoadEventKind.CycleEnd });
        await _db.SaveChangesAsync(ct);

        load.Status = LoadStatus.Completed;
        load.EndedAtUtc = endedAtUtc;
        LoadEvaluation evaluation;
        if (load.Kind == LoadKind.Survey)
        {
            var survey = await EvaluateSurveyAsync(load, recordInterval, ct);
            ApplySurvey(load, survey);
            evaluation = new LoadEvaluation
            {
                Passed = survey.Passed,
                SoakStartUtc = survey.StabilisedUtc,
                SoakEndUtc = endedAtUtc,
                SoakMinutes = survey.SurveyMinutes,
                Channels = Array.Empty<ChannelSummary>(),
                Findings = survey.Findings,
            };
        }
        else
        {
            evaluation = await EvaluateAsync(load, recordInterval, ct);
            Apply(load, evaluation);
        }
        AddOutbox("load.completed", load);
        await _db.SaveChangesAsync(ct);
        return evaluation;
    }

    public async Task<SurveyEvaluation> EvaluateSurveyAsync(Load load, TimeSpan recordInterval, CancellationToken ct = default)
    {
        var channels = load.Furnace!.Channels.Where(c => c.Enabled && c.Role == ChannelRole.Survey).Select(c => c.Channel).ToList();
        var events = await EventsAsync(load.Id, ct);
        var end = events.FirstOrDefault(e => e.Kind == LoadEventKind.DoorOpen).AtUtc is { Ticks: > 0 } door ? door : load.EndedAtUtc;
        return SurveyEvaluator.Evaluate(load.Recipe!.SetpointCelsius, load.Furnace.FurnaceClass, channels,
            await SamplesAsync(load.Id, ct), end, recordInterval);
    }

    public static IReadOnlyList<SurveySensorResult> SurveySensors(Load load) =>
        string.IsNullOrEmpty(load.SurveyJson) ? Array.Empty<SurveySensorResult>() : JsonSerializer.Deserialize<List<SurveySensorResult>>(load.SurveyJson)!;

    private static void ApplySurvey(Load load, SurveyEvaluation e)
    {
        load.Passed = e.Passed;
        load.SoakStartUtc = e.StabilisedUtc;
        load.SoakEndUtc = e.StabilisedUtc is null ? null : load.EndedAtUtc;
        load.SoakMinutes = e.SurveyMinutes;
        load.UniformityCelsius = e.UniformityCelsius;
        load.SurveyJson = JsonSerializer.Serialize(e.Sensors);
        load.FindingsJson = JsonSerializer.Serialize(e.Findings);
    }

    private void AddOutbox(string topic, Load load)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        _db.Outbox.Add(new OutboxMessage
        {
            Topic = topic,
            Key = load.LoadNumber,
            CreatedAtUtc = now,
            NextAttemptUtc = now,
            PayloadJson = JsonSerializer.Serialize(new
            {
                load.LoadNumber,
                Kind = load.Kind.ToString(),
                Status = load.Status.ToString(),
                load.Passed,
                load.StartedAtUtc,
                load.EndedAtUtc,
                load.SoakMinutes,
                load.DecidedBy,
                load.DecidedAtUtc,
                WorkOrders = load.Parts.Select(p => p.WorkOrder).Distinct().ToArray(),
            }),
        });
    }

    public async Task<LoadEvaluation> EvaluateAsync(Load load, TimeSpan recordInterval, CancellationToken ct = default)
    {
        var channels = load.Furnace!.Channels.Where(c => c.Enabled).Select(c => new EvaluationChannel(c.Channel, c.Role)).ToList();
        return LoadEvaluator.Evaluate(load.Recipe!.ToSpec(), load.Furnace.FurnaceClass, channels,
            await SamplesAsync(load.Id, ct), await EventsAsync(load.Id, ct), recordInterval);
    }

    public async Task DecideAsync(string loadNumber, bool release, string user, string? comment, byte[] rowVersion, CancellationToken ct = default)
    {
        var load = await _db.Loads.Include(l => l.Furnace).Include(l => l.Parts)
            .FirstOrDefaultAsync(l => l.LoadNumber == loadNumber, ct) ?? throw new LoadException($"Load {loadNumber} not found.");
        if (load.Status != LoadStatus.Completed) throw new LoadException($"Load {loadNumber} is {load.Status} and cannot be decided.");
        if (release && load.Passed != true && string.IsNullOrWhiteSpace(comment))
        {
            throw new LoadException("A comment is required to release a load with findings.");
        }
        if (!release && string.IsNullOrWhiteSpace(comment)) throw new LoadException("A comment is required to reject a load.");

        _db.Entry(load).Property(l => l.RowVersion).OriginalValue = rowVersion;
        load.Status = release ? LoadStatus.Released : LoadStatus.Rejected;
        load.DecidedBy = user;
        load.DecidedAtUtc = _time.GetUtcNow().UtcDateTime;
        load.DecisionComment = comment?.Trim();
        if (release && load.Kind == LoadKind.Survey && load.Passed == true)
        {
            var today = DateOnly.FromDateTime(load.DecidedAtUtc.Value);
            load.Furnace!.UniformitySurveyDue = today.AddMonths(SurveyEvaluator.IntervalMonths(load.Furnace.FurnaceClass, load.Furnace.Instrumentation));
        }
        AddOutbox(release ? "load.released" : "load.rejected", load);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new LoadException($"Load {loadNumber} was changed by someone else. Reload and try again.");
        }
    }

    public static IReadOnlyList<Finding> Findings(Load load) =>
        string.IsNullOrEmpty(load.FindingsJson) ? Array.Empty<Finding>() : JsonSerializer.Deserialize<List<Finding>>(load.FindingsJson)!;

    private static void Apply(Load load, LoadEvaluation e)
    {
        load.Passed = e.Passed;
        load.SoakStartUtc = e.SoakStartUtc;
        load.SoakEndUtc = e.SoakEndUtc;
        load.SoakMinutes = e.SoakMinutes;
        load.MaxRampCelsiusPerMinute = e.MaxRampCelsiusPerMinute;
        load.QuenchDelaySeconds = e.QuenchDelaySeconds;
        load.FindingsJson = JsonSerializer.Serialize(e.Findings);
    }
}
