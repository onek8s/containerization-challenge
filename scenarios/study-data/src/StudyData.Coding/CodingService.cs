using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudyData.Core.Coding;
using StudyData.Core.Queries;
using StudyData.Core.Services;
using StudyData.Data;
using StudyData.Data.Services;

namespace StudyData.Coding;

/// <summary>Autocoding of pending verbatim terms and the coder's manual decisions.</summary>
public class CodingService
{
    private readonly StudyDataContext _db;
    private readonly ICodingEngine _engine;
    private readonly AuditWriter _audit;
    private readonly QueryService _queries;
    private readonly IClock _clock;
    private readonly CodingOptions _options;
    private readonly ILogger<CodingService> _log;

    public CodingService(StudyDataContext db, ICodingEngine engine, AuditWriter audit, QueryService queries, IClock clock,
        IOptions<CodingOptions> options, ILogger<CodingService> log)
    {
        _db = db;
        _engine = engine;
        _audit = audit;
        _queries = queries;
        _clock = clock;
        _options = options.Value;
        _log = log;
    }

    public async Task<(int AutoCoded, int ForReview)> AutoCodeAsync(CancellationToken ct = default)
    {
        var pending = await _db.Coding.Where(c => c.State == CodingState.Pending).OrderBy(c => c.Id).Take(_options.BatchSize).ToListAsync(ct);
        var auto = 0;
        foreach (var item in pending)
        {
            var candidates = _engine.Search(item.Dictionary, item.Verbatim, _options.MaxCandidates);
            item.State = CodingRules.Decide(candidates);
            item.DictionaryVersion = _engine.DictionaryVersion(item.Dictionary);
            if (item.State == CodingState.AutoCoded)
            {
                Apply(item, candidates[0], "autocoder");
                auto++;
            }
            else
            {
                item.Score = candidates.FirstOrDefault()?.Score;
            }
        }

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Coding batch: {Auto} autocoded, {Review} for review", auto, pending.Count - auto);
        return (auto, pending.Count - auto);
    }

    public IReadOnlyList<CodingCandidate> Candidates(CodingItem item) =>
        _engine.Search(item.Dictionary, item.Verbatim, _options.MaxCandidates * 2);

    public async Task<CodingItem> AssignAsync(int itemId, string code, string user, CancellationToken ct = default)
    {
        var item = await _db.Coding.FirstOrDefaultAsync(c => c.Id == itemId, ct) ?? throw new SubjectRuleException("Unknown coding item.");
        var candidate = Candidates(item).FirstOrDefault(c => c.Code == code)
            ?? _engine.Search(item.Dictionary, code, 1).FirstOrDefault(c => c.Code == code)
            ?? throw new SubjectRuleException($"{code} is not a candidate for '{item.Verbatim}'.");
        item.DictionaryVersion = _engine.DictionaryVersion(item.Dictionary);
        Apply(item, candidate, user);
        item.State = CodingState.Coded;
        await _db.SaveChangesAsync(ct);
        return item;
    }

    public async Task<CodingItem> RaiseQueryAsync(int itemId, string text, string user, CancellationToken ct = default)
    {
        var item = await _db.Coding.FirstOrDefaultAsync(c => c.Id == itemId, ct) ?? throw new SubjectRuleException("Unknown coding item.");
        await _queries.OpenAsync(item.FormInstanceId, item.FieldCode, text, QueryOrigin.Coding, user, ct);
        item.State = CodingState.QueryRaised;
        await _db.SaveChangesAsync(ct);
        return item;
    }

    private void Apply(CodingItem item, CodingCandidate c, string user)
    {
        var old = item.Code;
        item.Code = c.Code;
        item.Term = c.Term;
        item.Hierarchy = $"{c.PreferredCode} {c.PreferredTerm} | {c.Hierarchy}";
        item.Score = c.Score;
        item.CodedBy = user;
        item.CodedAt = _clock.UtcNow;
        _audit.Add(user, "Coding", $"{item.Dictionary}/{item.Id}", item.SubjectId, "Coded", old, $"{c.Code} {c.Term}", item.DictionaryVersion);
    }
}
