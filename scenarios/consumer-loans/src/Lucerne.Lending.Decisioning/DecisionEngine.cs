using Lucerne.Lending.Domain;
using Newtonsoft.Json;
using RulesEngine.Models;

namespace Lucerne.Lending.Decisioning;

public enum DecisionOutcome
{
    Approve,
    Refer,
    Decline
}

public sealed record ReasonCode(string Code, string Text);

public sealed record RuleTrace(string Workflow, string Rule, bool Triggered, string? Error);

public sealed class DecisionResult
{
    public DecisionOutcome Outcome { get; init; }
    public RiskGrade Grade { get; init; }
    public int Score { get; init; }
    public IReadOnlyList<ReasonCode> Reasons { get; init; } = Array.Empty<ReasonCode>();
    public IReadOnlyList<ScoreLine> ScoreLines { get; init; } = Array.Empty<ScoreLine>();
    public IReadOnlyList<RuleTrace> Trace { get; init; } = Array.Empty<RuleTrace>();
    public string PolicyVersion { get; init; } = "";
}

/// <summary>Credit policy CP-CL-04: knock-out rules, scorecard, referral rules and grade limits.</summary>
public sealed class DecisionEngine
{
    public const string PolicyVersion = "CP-CL-04/2023.2";
    private readonly RulesEngine.RulesEngine _rules;
    private readonly Scorecard _scorecard;

    public DecisionEngine(string? policyJson = null, Scorecard? scorecard = null)
    {
        policyJson ??= LoadEmbedded("credit-policy.json");
        var workflows = JsonConvert.DeserializeObject<Workflow[]>(policyJson)!;
        _rules = new RulesEngine.RulesEngine(workflows, new ReSettings { CustomTypes = new[] { typeof(Math) } });
        _scorecard = scorecard ?? Scorecard.LoadEmbedded();
    }

    public string Version => $"{PolicyVersion} {_scorecard.Version}";

    public async Task<DecisionResult> DecideAsync(CreditFacts facts)
    {
        var trace = new List<RuleTrace>();
        var knockOuts = await RunAsync("KnockOut", facts, trace);
        var (score, lines) = _scorecard.Score(facts);
        var grade = _scorecard.Grade(score);

        if (knockOuts.Count > 0)
            return Result(DecisionOutcome.Decline, RiskGrade.X, score, knockOuts, lines, trace);

        if (score < _scorecard.CutOff || grade == RiskGrade.X)
            return Result(DecisionOutcome.Decline, RiskGrade.X, score, new[] { new ReasonCode("SC01", $"Score {score} below cut-off {_scorecard.CutOff}") }, lines, trace);

        var refer = await RunAsync("Refer", facts, trace);
        var limit = _scorecard.MaxAmount(grade);
        if (facts.Amount > limit)
            refer.Add(new ReasonCode("RF12", $"Amount over grade {grade} limit {limit:0} EUR"));

        return Result(refer.Count > 0 ? DecisionOutcome.Refer : DecisionOutcome.Approve, grade, score, refer, lines, trace);
    }

    private async Task<List<ReasonCode>> RunAsync(string workflow, CreditFacts facts, List<RuleTrace> trace)
    {
        var results = await _rules.ExecuteAllRulesAsync(workflow, facts);
        var triggered = new List<ReasonCode>();
        foreach (var r in results)
        {
            var error = string.IsNullOrEmpty(r.ExceptionMessage) ? null : r.ExceptionMessage;
            if (error != null) throw new InvalidOperationException($"Rule {workflow}/{r.Rule.RuleName} failed: {error}");
            trace.Add(new RuleTrace(workflow, r.Rule.RuleName, r.IsSuccess, error));
            if (r.IsSuccess) triggered.Add(new ReasonCode(r.Rule.RuleName, r.Rule.SuccessEvent));
        }
        return triggered;
    }

    private DecisionResult Result(DecisionOutcome outcome, RiskGrade grade, int score, IEnumerable<ReasonCode> reasons,
        IReadOnlyList<ScoreLine> lines, IReadOnlyList<RuleTrace> trace) => new()
    {
        Outcome = outcome,
        Grade = grade,
        Score = score,
        Reasons = reasons.ToList(),
        ScoreLines = lines,
        Trace = trace,
        PolicyVersion = Version
    };

    private static string LoadEmbedded(string name)
    {
        using var stream = typeof(DecisionEngine).Assembly.GetManifestResourceStream("Lucerne.Lending.Decisioning.Rules." + name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
