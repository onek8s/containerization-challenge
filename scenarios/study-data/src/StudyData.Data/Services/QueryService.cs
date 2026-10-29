using Microsoft.EntityFrameworkCore;
using StudyData.Core.Documents;
using StudyData.Core.Queries;
using StudyData.Core.Services;

namespace StudyData.Data.Services;

public record QueryRow(int Id, int SubjectId, string Subject, string Site, string Visit, string Form, int Row, string FieldCode,
    QueryState State, QueryOrigin Origin, string Text, DateTime OpenedAt, string OpenedBy, int AgeDays, int FormInstanceId);

public class QueryService
{
    private readonly StudyDataContext _db;
    private readonly AuditWriter _audit;
    private readonly IClock _clock;

    public QueryService(StudyDataContext db, AuditWriter audit, IClock clock)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
    }

    public async Task<List<QueryRow>> ListAsync(int studyId, IReadOnlyCollection<int>? siteIds, QueryState? state, int? subjectId = null, CancellationToken ct = default)
    {
        var q = from query in _db.Queries
                join form in _db.Forms on query.FormInstanceId equals form.Id
                join visit in _db.Visits on form.VisitId equals visit.Id
                join subject in _db.Subjects on visit.SubjectId equals subject.Id
                where subject.StudyId == studyId
                select new { query, form, visit, subject };
        if (siteIds is not null)
        {
            q = q.Where(x => siteIds.Contains(x.subject.SiteId));
        }

        if (state is not null)
        {
            q = q.Where(x => x.query.State == state);
        }

        if (subjectId is not null)
        {
            q = q.Where(x => x.subject.Id == subjectId);
        }

        var now = _clock.UtcNow;
        var rows = await q.Select(x => new
        {
            x.query.Id, SubjectId = x.subject.Id, Subject = x.subject.Number, Site = x.subject.Site!.Number,
            Visit = x.visit.Definition!.Code, Form = x.form.Definition!.Code, x.form.Row, x.query.FieldCode, x.query.State,
            x.query.Origin, x.query.Text, x.query.OpenedAt, x.query.OpenedBy, FormInstanceId = x.form.Id
        }).ToListAsync(ct);

        return rows
            .Select(r => new QueryRow(r.Id, r.SubjectId, r.Subject, r.Site, r.Visit, r.Form, r.Row, r.FieldCode, r.State, r.Origin, r.Text,
                r.OpenedAt, r.OpenedBy, (int)(now - r.OpenedAt).TotalDays, r.FormInstanceId))
            .OrderBy(r => r.State).ThenBy(r => r.OpenedAt)
            .ToList();
    }

    public Task<Query?> GetAsync(int id, CancellationToken ct = default) =>
        _db.Queries.Include(q => q.Messages).FirstOrDefaultAsync(q => q.Id == id, ct);

    public async Task<Query> OpenAsync(int formInstanceId, string fieldCode, string text, QueryOrigin origin, string user, CancellationToken ct = default)
    {
        var form = await _db.Forms.Include(f => f.Visit).ThenInclude(v => v!.Subject).FirstOrDefaultAsync(f => f.Id == formInstanceId, ct)
            ?? throw new SubjectRuleException("Unknown form.");
        var subject = form.Visit!.Subject!;
        var q = QueryWorkflow.Open(subject.Id, form.Id, fieldCode, origin, text, user, _clock.UtcNow);
        _db.Queries.Add(q);
        _audit.Add(user, "Query", $"{subject.Number}/{fieldCode}", subject.Id, "Opened", newValue: text);
        Notify(subject.StudyId, subject.SiteId, subject.Id, "QueryOpened", $"{subject.Number} {fieldCode}: {text}");
        await _db.SaveChangesAsync(ct);
        return q;
    }

    public Task<Query> AnswerAsync(int id, string text, string user, CancellationToken ct = default) =>
        TransitionAsync(id, user, "Answered", text, q => QueryWorkflow.Answer(q, text, user, _clock.UtcNow), ct);

    public Task<Query> CloseAsync(int id, string? text, string user, CancellationToken ct = default) =>
        TransitionAsync(id, user, "Closed", text, q => QueryWorkflow.Close(q, text, user, _clock.UtcNow), ct);

    public Task<Query> ReopenAsync(int id, string text, string user, CancellationToken ct = default) =>
        TransitionAsync(id, user, "Reopened", text, q => QueryWorkflow.Reopen(q, text, user, _clock.UtcNow), ct);

    public Task<Query> CancelAsync(int id, string text, string user, CancellationToken ct = default) =>
        TransitionAsync(id, user, "Cancelled", text, q => QueryWorkflow.Cancel(q, text, user, _clock.UtcNow), ct);

    private async Task<Query> TransitionAsync(int id, string user, string action, string? text, Action<Query> transition, CancellationToken ct)
    {
        var q = await GetAsync(id, ct) ?? throw new SubjectRuleException("Unknown query.");
        var subject = await _db.Subjects.FirstAsync(s => s.Id == q.SubjectId, ct);
        var old = q.State;
        transition(q);
        _audit.Add(user, "Query", id.ToString(), q.SubjectId, action, old.ToString(), q.State.ToString(), text);
        Notify(subject.StudyId, subject.SiteId, subject.Id, "Query" + action, $"{subject.Number} query {id} {action.ToLowerInvariant()} by {user}");
        await _db.SaveChangesAsync(ct);
        return q;
    }

    private void Notify(int studyId, int siteId, int subjectId, string kind, string text) =>
        _db.Notifications.Add(new Notification { At = _clock.UtcNow, StudyId = studyId, SiteId = siteId, SubjectId = subjectId, Kind = kind, Text = text });
}
