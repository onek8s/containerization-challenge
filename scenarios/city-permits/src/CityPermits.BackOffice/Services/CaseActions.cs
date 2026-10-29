using System.Text.Json;
using CityPermits.Archive;
using CityPermits.Contracts;
using CityPermits.Data;
using CityPermits.Documents;
using CityPermits.Domain;
using CityPermits.Domain.Inspections;
using CityPermits.Domain.Model;
using CityPermits.Domain.Rules;
using CityPermits.Domain.Workflow;
using CityPermits.Gis;
using Microsoft.EntityFrameworkCore;

namespace CityPermits.BackOffice.Services;

/// <summary>Officer actions that touch more than the case row: site checks, decisions, issuing.</summary>
public sealed class CaseActions
{
    private readonly PermitsDbContext _db;
    private readonly IGisService _gis;
    private readonly IDocumentArchive _archive;
    private readonly PermitDocuments _documents;
    private readonly OutboxWriter _outbox;
    private readonly ReferenceAllocator _refs;

    public CaseActions(PermitsDbContext db, IGisService gis, IDocumentArchive archive, PermitDocuments documents, OutboxWriter outbox, ReferenceAllocator refs)
    {
        _db = db; _gis = gis; _archive = archive; _documents = documents; _outbox = outbox; _refs = refs;
    }

    public Task<PermitCase?> LoadAsync(int id) => _db.Cases
        .Include(c => c.Applicant).Include(c => c.Fees).Include(c => c.Payments).Include(c => c.Events)
        .Include(c => c.Documents).Include(c => c.Conditions).Include(c => c.Inspections).Include(c => c.Consultations)
        .AsSplitQuery().FirstOrDefaultAsync(c => c.Id == id);

    public static SiteConstraints? Site(PermitCase c) =>
        c.SpatialCheckJson is null ? null : JsonSerializer.Deserialize<SiteConstraints>(c.SpatialCheckJson);

    public IReadOnlyList<RuleFinding> RunSiteChecks(PermitCase c, string actor)
    {
        if (c.ParcelId is null) return Array.Empty<RuleFinding>();
        var site = _gis.CheckSite(new SiteCheckRequest(c.ParcelId, c.Building?.FootprintWkt));
        c.SpatialCheckJson = JsonSerializer.Serialize(site);
        c.SpatialCheckedUtc = DateTime.UtcNow;
        var findings = PlanningRules.Evaluate(c, site);
        c.Events.Add(new CaseEvent { AtUtc = DateTime.UtcNow, Actor = actor, Type = "site-checks", Text = $"{findings.Count} findings ({site.Source})" });
        foreach (var f in findings.Where(f => f.Consultee is not null && f.Severity == FindingSeverity.Consult))
            if (!c.Consultations.Any(x => x.Consultee == f.Consultee))
                c.Consultations.Add(new Consultation { Consultee = f.Consultee!, Reason = f.Message, SentUtc = DateTime.UtcNow, DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(21)) });
        return findings;
    }

    /// <summary>Issues an approved case: validity, decision letter with site plan, permit and inspections.</summary>
    public async Task IssueAsync(PermitCase c, string actor)
    {
        if (c.Status == CaseStatus.Approved)
        {
            PermitCase? previous = c.RenewalOfCaseId is { } pid ? await _db.Cases.FindAsync(pid) : null;
            var (from, to) = RenewalPolicy.Validity(c, DateOnly.FromDateTime(DateTime.UtcNow), previous);
            c.ValidFrom = from;
            c.ValidTo = to;
            if (c.Parking is not null)
                c.Parking.PermitSerial = await _refs.NextSerialAsync("PS-" + c.Parking.ZoneCode, from.Year, c.Parking.ZoneCode + "{0:yy}-{1:00000}");
            CaseWorkflow.Apply(c, CaseWorkflow.Issue, actor, new[] { Roles.CaseOfficer }, null, DateTime.UtcNow);
            foreach (var stage in InspectionStages.For(c))
                c.Inspections.Add(new Inspection { Stage = stage, Status = InspectionStatus.Requested });
        }
        await AddDecisionDocumentsAsync(c, actor);
        await _db.SaveChangesAsync();
    }

    public async Task AddDecisionDocumentsAsync(PermitCase c, string actor)
    {
        byte[]? plan = null;
        if (c.ParcelId is not null && c.Kind == PermitKind.BuildingPermit)
            plan = _gis.ExportMap(new MapExportRequest("DecisionMap", c.ParcelId, c.Building?.FootprintWkt, c.Title, c.Reference)).Content;
        var docs = new List<GeneratedDocument> { _documents.DecisionLetter(c, plan) };
        if (c.Outcome != DecisionOutcome.Refused) docs.Add(_documents.PermitCertificate(c));
        foreach (var d in docs)
        {
            using var ms = new MemoryStream(d.Content);
            var a = await _archive.StoreAsync(c.Reference, d.FileName, ms);
            var doc = new CaseDocument
            {
                Kind = d.Kind, FileName = d.FileName, ContentType = d.ContentType, Length = a.Length, Sha256 = a.Sha256,
                ArchiveKey = a.Key, ArchivePath = a.Path, CreatedUtc = DateTime.UtcNow, CreatedBy = actor, VisibleToApplicant = true
            };
            c.Documents.Add(doc);
        }
        _outbox.Add(OutboxTypes.FileToRecords, new { c.Id });
        _outbox.Add(OutboxTypes.ApplicantNotification, new { c.Id, Template = c.Outcome == DecisionOutcome.Refused ? "refused" : "issued" });
    }
}
