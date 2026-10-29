using Microsoft.Extensions.Logging;
using NRules;
using NRules.Fluent;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Rules.Facts;

namespace Proseware.ControlTower.Rules;

public sealed class AlertFacts
{
    public DateTimeOffset Now { get; init; }
    public List<ShipmentFact> Shipments { get; } = new();
    public List<VehicleReadingFact> Vehicles { get; } = new();
    public List<CustomsFact> Customs { get; } = new();
    public List<DangerousGoodsFact> DangerousGoods { get; } = new();
    public List<ScanFact> Scans { get; } = new();
}

/// <summary>Compiles the rule sets once and evaluates facts in a fresh session per call.</summary>
public sealed class AlertEvaluator
{
    private readonly ISessionFactory _factory;
    private readonly ILogger<AlertEvaluator> _logger;

    public AlertEvaluator(ILogger<AlertEvaluator> logger)
    {
        _logger = logger;
        var repository = new RuleRepository();
        repository.Load(x => x.From(typeof(AlertEvaluator).Assembly));
        RuleNames = repository.GetRules().Select(r => r.Name).OrderBy(n => n).ToList();
        _factory = repository.Compile();
    }

    public IReadOnlyList<string> RuleNames { get; }

    public IReadOnlyList<AlertCandidate> Evaluate(AlertFacts facts)
    {
        var session = _factory.CreateSession();
        session.Events.RhsExpressionFailedEvent += (_, e) => _logger.LogError(e.Exception, "Rule action failed");

        session.Insert(new ClockFact(facts.Now));
        session.InsertAll(facts.Shipments);
        session.InsertAll(facts.Vehicles);
        session.InsertAll(facts.Customs);
        session.InsertAll(facts.DangerousGoods);
        session.InsertAll(facts.Scans);
        foreach (var load in VehicleLoads(facts)) session.Insert(load);

        var fired = session.Fire();
        var candidates = session.Query<AlertCandidate>().ToList()
            .GroupBy(c => (c.ShipmentId, c.RuleCode))
            .Select(g => g.OrderByDescending(c => c.Severity).First())
            .ToList();
        _logger.LogDebug("{Rules} rules fired, {Alerts} alert candidates for {Shipments} shipments", fired, candidates.Count, facts.Shipments.Count);
        return candidates;
    }

    private static IEnumerable<VehicleLoadFact> VehicleLoads(AlertFacts facts)
    {
        var vehicles = facts.Vehicles.ToDictionary(v => v.VehicleId);
        return facts.Shipments
            .Where(s => s.VehicleId is not null && s.IsMoving && s.Mode == TransportMode.Road)
            .GroupBy(s => s.VehicleId!.Value)
            .Where(g => vehicles.ContainsKey(g.Key))
            .Select(g => new VehicleLoadFact(g.Key, vehicles[g.Key].Plate, g.Sum(s => s.GrossWeightKg), vehicles[g.Key].MaxPayloadKg, g.Select(s => s.ShipmentId).ToList()));
    }
}
