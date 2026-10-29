using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StudyData.Coding;
using StudyData.Core.Coding;
using StudyData.Core.Design;
using StudyData.Core.Documents;
using StudyData.Core.Queries;
using StudyData.Core.Services;
using StudyData.Core.Subjects;
using StudyData.Data;
using StudyData.Data.Services;
using StudyData.Monitor.Api.Services;

namespace StudyData.Monitor.Api.Endpoints;

public record TextRequest(string? Text);

public record OpenQueryRequest(string Field, string Text);

public record VerifyRequest(string[]? Fields);

public record AssignCodeRequest(string Code);

public static class MonitorApi
{
    public static RouteGroupBuilder MapMonitorApi(this RouteGroupBuilder api)
    {
        api.MapGet("/me", (ClaimsPrincipal user) => new
        {
            name = user.Identity?.Name,
            roles = user.FindAll(ClaimTypes.Role).Concat(user.FindAll("role")).Select(c => c.Value).Distinct()
        });

        var review = api.MapGroup("").RequireAuthorization(Policies.Review);

        review.MapGet("/studies", async (StudyDataContext db) =>
            await db.Studies.OrderBy(s => s.Code).Select(s => new { s.Id, s.Code, s.Title, s.Sponsor, s.Phase, Status = s.Status.ToString(), s.TargetEnrolment }).ToListAsync());

        review.MapGet("/studies/{studyId:int}/sites", async (int studyId, StudyDataContext db) =>
        {
            var sites = await db.Sites.Where(s => s.StudyId == studyId).OrderBy(s => s.Number).ToListAsync();
            var subjects = await db.Subjects.Where(s => s.StudyId == studyId).GroupBy(s => s.SiteId)
                .Select(g => new { g.Key, Total = g.Count(), Randomised = g.Count(s => s.RandomisedAt != null) }).ToListAsync();
            var toVerify = await (from v in db.Values
                                  join f in db.Forms on v.FormInstanceId equals f.Id
                                  join vis in db.Visits on f.VisitId equals vis.Id
                                  join s in db.Subjects on vis.SubjectId equals s.Id
                                  where s.StudyId == studyId && !v.Verified && f.Status >= FormStatus.Complete
                                  group v by s.SiteId into g
                                  select new { g.Key, Count = g.Count() }).ToListAsync();
            var queries = await (from q in db.Queries
                                 join s in db.Subjects on q.SubjectId equals s.Id
                                 where s.StudyId == studyId && (q.State == QueryState.Open || q.State == QueryState.Answered)
                                 group q by new { s.SiteId, q.State } into g
                                 select new { g.Key.SiteId, g.Key.State, Count = g.Count() }).ToListAsync();
            return sites.Select(s => new
            {
                s.Id, s.Number, s.Name, s.Investigator, s.Country,
                subjects = subjects.FirstOrDefault(x => x.Key == s.Id)?.Total ?? 0,
                randomised = subjects.FirstOrDefault(x => x.Key == s.Id)?.Randomised ?? 0,
                valuesToVerify = toVerify.FirstOrDefault(x => x.Key == s.Id)?.Count ?? 0,
                openQueries = queries.Where(q => q.SiteId == s.Id && q.State == QueryState.Open).Sum(q => q.Count),
                answeredQueries = queries.Where(q => q.SiteId == s.Id && q.State == QueryState.Answered).Sum(q => q.Count)
            });
        });

        review.MapGet("/sites/{siteId:int}/subjects", async (int siteId, StudyDataContext db) =>
            await db.Subjects.Where(s => s.SiteId == siteId).OrderBy(s => s.Number)
                .Select(s => new { s.Id, s.Number, Status = s.Status.ToString(), s.ConsentDate, s.RandomisationNumber, s.RandomisedAt })
                .ToListAsync());

        review.MapGet("/subjects/{subjectId:int}", async (int subjectId, StudyDataContext db, StudyReadService read) =>
        {
            var subject = await read.GetCasebookAsync(subjectId);
            if (subject is null)
            {
                return Results.NotFound();
            }

            var study = (await read.GetDesignAsync(subject.StudyId))!;
            var queries = await db.Queries.Include(q => q.Messages).Where(q => q.SubjectId == subjectId).ToListAsync();
            var coding = await db.Coding.Where(c => c.SubjectId == subjectId).ToListAsync();
            return Results.Ok(new
            {
                subject.Id, subject.Number, Status = subject.Status.ToString(), site = new { subject.Site!.Id, subject.Site.Number, subject.Site.Name },
                study = new { study.Id, study.Code },
                subject.RandomisationNumber, subject.ConsentDate,
                visits = subject.Visits.OrderBy(v => v.Definition!.Order).ThenBy(v => v.Repeat).Select(v => new
                {
                    v.Id, v.Definition!.Code, v.Definition.Name, v.Date, Status = v.Status.ToString(), v.OutOfWindow,
                    forms = v.Forms.OrderBy(f => study.Forms.First(d => d.Id == f.FormDefinitionId).Order).ThenBy(f => f.Row).Select(f =>
                    {
                        var def = study.Forms.First(d => d.Id == f.FormDefinitionId);
                        return new
                        {
                            f.Id, def.Code, def.Name, f.Row, Status = f.Status.ToString(), f.LastSavedAt, f.LastSavedBy, f.SignedBy, f.DerivedWith,
                            fields = def.Fields.OrderBy(x => x.Order).Select(x =>
                            {
                                var value = f.Values.FirstOrDefault(v => v.FieldCode == x.Code);
                                var code = coding.FirstOrDefault(c => c.FormInstanceId == f.Id && c.FieldCode == x.Code);
                                return new
                                {
                                    x.Code, x.Label, Type = x.Type.ToString(), x.Unit, x.Sdv,
                                    value = Display(x, value?.Value), raw = value?.Value,
                                    verified = value?.Verified ?? false, value?.VerifiedBy,
                                    coding = code is null ? null : new { State = code.State.ToString(), code.Code, code.Term, code.Hierarchy }
                                };
                            }),
                            queries = queries.Where(q => q.FormInstanceId == f.Id).Select(q => new
                            {
                                q.Id, q.FieldCode, State = q.State.ToString(), Origin = q.Origin.ToString(), q.Text, q.OpenedAt, q.OpenedBy,
                                messages = q.Messages.OrderBy(m => m.At).Select(m => new { m.At, m.User, m.Role, m.Text })
                            })
                        };
                    })
                })
            });
        });

        review.MapGet("/subjects/{subjectId:int}/audit", async (int subjectId, StudyDataContext db) =>
            await db.Audit.Where(a => a.SubjectId == subjectId).OrderByDescending(a => a.Id).Take(500).ToListAsync());

        review.MapGet("/subjects/{subjectId:int}/labs", async (int subjectId, StudyDataContext db) =>
            await db.LabResults.Where(l => l.SubjectId == subjectId).OrderBy(l => l.CollectedAt).ThenBy(l => l.TestCode)
                .Select(l => new { l.VisitCode, l.CollectedAt, l.TestCode, l.TestName, l.Value, l.Unit, l.LowerLimit, l.UpperLimit, Flag = l.Flag.ToString(), l.Grade, l.Accession })
                .ToListAsync());

        review.MapPost("/forms/{formId:int}/verify", (int formId, VerifyRequest request, ReviewService reviewService, ClaimsPrincipal user) =>
            Rule(async () => Results.Ok(new { verified = await reviewService.VerifyAsync(formId, request.Fields, user.Identity!.Name!) })));

        review.MapPost("/forms/{formId:int}/queries", (int formId, OpenQueryRequest request, QueryService queries, ClaimsPrincipal user) =>
            Rule(async () =>
            {
                var origin = user.IsInRole(Roles.Monitor) ? QueryOrigin.Monitor : QueryOrigin.DataManager;
                var q = await queries.OpenAsync(formId, request.Field, request.Text, origin, user.Identity!.Name!);
                return Results.Ok(new { q.Id });
            }));

        review.MapGet("/studies/{studyId:int}/queries", async (int studyId, QueryState? state, int? siteId, QueryService queries) =>
            await queries.ListAsync(studyId, siteId is null ? null : new[] { siteId.Value }, state));

        review.MapPost("/queries/{id:int}/close", (int id, TextRequest r, QueryService q, ClaimsPrincipal u) => Rule(async () => Results.Ok(Summary(await q.CloseAsync(id, r.Text, u.Identity!.Name!)))));
        review.MapPost("/queries/{id:int}/reopen", (int id, TextRequest r, QueryService q, ClaimsPrincipal u) => Rule(async () => Results.Ok(Summary(await q.ReopenAsync(id, r.Text ?? "", u.Identity!.Name!)))));
        review.MapPost("/queries/{id:int}/cancel", (int id, TextRequest r, QueryService q, ClaimsPrincipal u) => Rule(async () => Results.Ok(Summary(await q.CancelAsync(id, r.Text ?? "", u.Identity!.Name!)))));

        review.MapGet("/subjects/{subjectId:int}/casebook", async (int subjectId, StudyDataContext db, StudyReadService read, CasebookPageCache cache, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var pages = await cache.GetAsync(db, read, subjectId, user.Identity!.Name!, ct);
            return Results.Ok(new { pages = pages.Count, contentType = pages.FirstOrDefault()?.ContentType });
        });

        review.MapGet("/subjects/{subjectId:int}/casebook/pages/{page:int}", async (int subjectId, int page, StudyDataContext db, StudyReadService read,
            CasebookPageCache cache, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var pages = await cache.GetAsync(db, read, subjectId, user.Identity!.Name!, ct);
            var p = pages.FirstOrDefault(x => x.Number == page);
            return p is null ? Results.NotFound() : Results.File(p.Content, p.ContentType);
        });

        review.MapPost("/subjects/{subjectId:int}/casebook/archive", async (int subjectId, StudyDataContext db, IClock clock, ClaimsPrincipal user) =>
        {
            var subject = await db.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId);
            if (subject is null)
            {
                return Results.NotFound();
            }

            var doc = new GeneratedDocument
            {
                StudyId = subject.StudyId, SubjectId = subjectId, Kind = "Casebook", State = JobState.Requested,
                RequestedBy = user.Identity!.Name!, RequestedAt = clock.UtcNow
            };
            db.GeneratedDocuments.Add(doc);
            await db.SaveChangesAsync();
            return Results.Accepted($"/api/documents/{doc.Id}", new { doc.Id, State = doc.State.ToString() });
        });

        review.MapGet("/documents/{id:int}", async (int id, StudyDataContext db) =>
            await db.GeneratedDocuments.FirstOrDefaultAsync(d => d.Id == id) is { } d
                ? Results.Ok(new { d.Id, d.Kind, State = d.State.ToString(), d.RequestedAt, d.CompletedAt, d.Error, file = d.Path is null ? null : Path.GetFileName(d.Path) })
                : Results.NotFound());

        review.MapGet("/documents/{id:int}/file", async (int id, StudyDataContext db) =>
        {
            var d = await db.GeneratedDocuments.FirstOrDefaultAsync(x => x.Id == id);
            if (d?.Path is null || !File.Exists(d.Path))
            {
                return Results.NotFound();
            }

            var type = Path.GetExtension(d.Path).ToLowerInvariant() switch { ".pdf" => "application/pdf", ".html" => "text/html", ".zip" => "application/zip", _ => "application/octet-stream" };
            return Results.File(File.OpenRead(d.Path), type, Path.GetFileName(d.Path));
        });

        var dm = api.MapGroup("").RequireAuthorization(Policies.DataManagement);
        dm.MapPost("/forms/{formId:int}/lock", (int formId, ReviewService r, ClaimsPrincipal u) => Rule(async () => { await r.LockAsync(formId, true, u.Identity!.Name!); return Results.NoContent(); }));
        dm.MapPost("/forms/{formId:int}/unlock", (int formId, ReviewService r, ClaimsPrincipal u) => Rule(async () => { await r.LockAsync(formId, false, u.Identity!.Name!); return Results.NoContent(); }));

        var coding = api.MapGroup("/coding").RequireAuthorization(Policies.Coding);
        coding.MapGet("", async (CodingState? state, StudyDataContext db) =>
        {
            var s = state ?? CodingState.NeedsReview;
            return await (from c in db.Coding
                          join subj in db.Subjects on c.SubjectId equals subj.Id
                          where c.State == s
                          orderby c.Id
                          select new { c.Id, c.Dictionary, c.Verbatim, State = c.State.ToString(), c.Code, c.Term, c.Score, Subject = subj.Number, c.SubjectId })
                .Take(500).ToListAsync();
        });
        coding.MapGet("/{id:int}/candidates", async (int id, StudyDataContext db, CodingService service) =>
            await db.Coding.FirstOrDefaultAsync(c => c.Id == id) is { } item ? Results.Ok(service.Candidates(item)) : Results.NotFound());
        coding.MapPost("/{id:int}/assign", (int id, AssignCodeRequest r, CodingService s, ClaimsPrincipal u) =>
            Rule(async () => { var item = await s.AssignAsync(id, r.Code, u.Identity!.Name!); return Results.Ok(new { item.Id, item.Code, item.Term }); }));
        coding.MapPost("/{id:int}/query", (int id, TextRequest r, CodingService s, ClaimsPrincipal u) =>
            Rule(async () => { await s.RaiseQueryAsync(id, r.Text ?? "Please clarify the verbatim term.", u.Identity!.Name!); return Results.NoContent(); }));
        coding.MapPost("/run", async (CodingService s) => { var (auto, review) = await s.AutoCodeAsync(); return Results.Ok(new { auto, review }); });

        api.MapGet("/notifications", async (int studyId, StudyDataContext db) =>
            await db.Notifications.Where(n => n.StudyId == studyId).OrderByDescending(n => n.Id).Take(50).ToListAsync()).RequireAuthorization(Policies.Review);

        return api;
    }

    private static object Summary(Query q) => new { q.Id, State = q.State.ToString() };

    private static async Task<IResult> Rule(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is SubjectRuleException or QueryTransitionException)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static string? Display(FieldDefinition f, string? raw) =>
        raw is null ? null
        : f.Type == FieldType.Choice ? f.ChoiceList().FirstOrDefault(c => c.Key == raw).Value ?? raw
        : f.Type == FieldType.Boolean ? (raw == "1" ? "Yes" : "No")
        : raw;
}
