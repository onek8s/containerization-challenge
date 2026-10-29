using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StudyData.Core.Design;
using StudyData.Core.Documents;
using StudyData.Core.Services;
using StudyData.Core.Subjects;
using StudyData.Data;

namespace StudyData.Sponsor.Api;

public record ExportRequest(bool IncludeArm = false);

/// <summary>The calling sponsor system (ADFS client id) and the studies it may read.</summary>
public class SponsorAccess
{
    private readonly StudyDataContext _db;
    private readonly IConfiguration _configuration;

    public SponsorAccess(StudyDataContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<(SponsorClient? Client, Study? Study)> ResolveAsync(ClaimsPrincipal user, string studyCode)
    {
        var claim = _configuration["Sponsor:ClientIdClaim"] ?? "appid";
        var clientId = user.FindFirstValue(claim) ?? user.FindFirstValue("client_id");
        var client = await _db.SponsorClients.FirstOrDefaultAsync(c => c.ClientId == clientId && c.Active);
        if (client is null || !client.Allows(studyCode))
        {
            return (client, null);
        }

        return (client, await _db.Studies.FirstOrDefaultAsync(s => s.Code == studyCode));
    }
}

public static class SponsorApi
{
    public static RouteGroupBuilder MapSponsorApi(this RouteGroupBuilder api)
    {
        api.MapGet("/studies/{code}", async (string code, ClaimsPrincipal user, SponsorAccess access, StudyDataContext db) =>
        {
            var (_, study) = await access.ResolveAsync(user, code);
            if (study is null)
            {
                return Results.NotFound();
            }

            var sites = await db.Sites.Where(s => s.StudyId == study.Id).OrderBy(s => s.Number).ToListAsync();
            var counts = await db.Subjects.Where(s => s.StudyId == study.Id).GroupBy(s => new { s.SiteId, s.Status })
                .Select(g => new { g.Key.SiteId, g.Key.Status, Count = g.Count() }).ToListAsync();
            return Results.Ok(new
            {
                study.Code, study.Title, study.Phase, Status = study.Status.ToString(), study.TargetEnrolment,
                screened = counts.Sum(c => c.Count),
                randomised = counts.Where(c => c.Status is not (SubjectStatus.Screening or SubjectStatus.ScreenFailed)).Sum(c => c.Count),
                sites = sites.Select(s => new
                {
                    s.Number, s.Name, s.Country, s.Active,
                    byStatus = counts.Where(c => c.SiteId == s.Id).ToDictionary(c => c.Status.ToString(), c => c.Count)
                })
            });
        }).WithName("GetStudy");

        api.MapGet("/studies/{code}/subjects", async (string code, ClaimsPrincipal user, SponsorAccess access, StudyDataContext db) =>
        {
            var (client, study) = await access.ResolveAsync(user, code);
            if (study is null)
            {
                return Results.NotFound();
            }

            var rows = await db.Subjects.Where(s => s.StudyId == study.Id).OrderBy(s => s.Number)
                .Select(s => new
                {
                    s.Number, Site = s.Site!.Number, Status = s.Status.ToString(), s.ConsentDate, s.RandomisedAt, s.RandomisationNumber,
                    Arm = client!.MayUnblind || !study.Blinded ? s.Arm!.Code : null
                }).ToListAsync();
            return Results.Ok(rows);
        }).WithName("ListSubjects");

        api.MapPost("/studies/{code}/exports", async (string code, ExportRequest request, ClaimsPrincipal user, SponsorAccess access, StudyDataContext db, IClock clock) =>
        {
            var (client, study) = await access.ResolveAsync(user, code);
            if (study is null)
            {
                return Results.NotFound();
            }

            if (request.IncludeArm && study.Blinded && !client!.MayUnblind)
            {
                return Results.Problem("This client may not receive treatment assignments.", statusCode: StatusCodes.Status403Forbidden);
            }

            var doc = new GeneratedDocument
            {
                StudyId = study.Id, Kind = "OdmExport", State = JobState.Requested, RequestedBy = client!.ClientId, RequestedAt = clock.UtcNow,
                Parameters = request.IncludeArm ? "includeArm" : null
            };
            db.GeneratedDocuments.Add(doc);
            await db.SaveChangesAsync();
            return Results.Accepted($"/api/v1/exports/{doc.Id}", new { doc.Id, State = doc.State });
        }).WithName("RequestExport");

        api.MapGet("/exports/{id:int}", (int id, ClaimsPrincipal user, SponsorAccess access, StudyDataContext db) => Document(id, "OdmExport", user, access, db, file: false)).WithName("GetExport");
        api.MapGet("/exports/{id:int}/file", (int id, ClaimsPrincipal user, SponsorAccess access, StudyDataContext db) => Document(id, "OdmExport", user, access, db, file: true)).WithName("DownloadExport");

        api.MapGet("/studies/{code}/safety-reports", async (string code, ClaimsPrincipal user, SponsorAccess access, StudyDataContext db) =>
        {
            var (_, study) = await access.ResolveAsync(user, code);
            if (study is null)
            {
                return Results.NotFound();
            }

            var reports = await db.GeneratedDocuments.Where(d => d.StudyId == study.Id && d.Kind == "SafetyWorkbook" && d.State == JobState.Completed)
                .OrderByDescending(d => d.CompletedAt).Select(d => new { d.Id, d.CompletedAt, d.Parameters, d.Sha256 }).ToListAsync();
            return Results.Ok(reports);
        }).WithName("ListSafetyReports");

        api.MapGet("/safety-reports/{id:int}/file", (int id, ClaimsPrincipal user, SponsorAccess access, StudyDataContext db) => Document(id, "SafetyWorkbook", user, access, db, file: true)).WithName("DownloadSafetyReport");

        return api;
    }

    private static async Task<IResult> Document(int id, string kind, ClaimsPrincipal user, SponsorAccess access, StudyDataContext db, bool file)
    {
        var doc = await db.GeneratedDocuments.FirstOrDefaultAsync(d => d.Id == id && d.Kind == kind);
        if (doc is null)
        {
            return Results.NotFound();
        }

        var code = await db.Studies.Where(s => s.Id == doc.StudyId).Select(s => s.Code).FirstAsync();
        var (_, study) = await access.ResolveAsync(user, code);
        if (study is null)
        {
            return Results.NotFound();
        }

        if (!file)
        {
            return Results.Ok(new { doc.Id, doc.State, doc.RequestedAt, doc.CompletedAt, doc.Error, doc.Sha256 });
        }

        if (doc.State != JobState.Completed || doc.Path is null || !File.Exists(doc.Path))
        {
            return Results.Problem("The file is not ready.", statusCode: StatusCodes.Status409Conflict);
        }

        var type = Path.GetExtension(doc.Path).ToLowerInvariant() switch
        {
            ".zip" => "application/zip",
            ".xml" => "application/xml",
            ".pdf" => "application/pdf",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };
        return Results.File(File.OpenRead(doc.Path), type, Path.GetFileName(doc.Path));
    }
}
