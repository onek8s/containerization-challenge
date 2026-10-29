using Microsoft.EntityFrameworkCore;
using StudyData.Core.Design;
using StudyData.Core.Scheduling;
using StudyData.Core.Services;
using StudyData.Core.Subjects;

namespace StudyData.Data.Services;

public class SubjectRuleException : InvalidOperationException
{
    public SubjectRuleException(string message) : base(message)
    {
    }
}

public record ScreenRequest(int SiteId, string? Initials, int BirthYear, string Sex, DateOnly ConsentDate, string User);

public class SubjectService
{
    private readonly StudyDataContext _db;
    private readonly AuditWriter _audit;
    private readonly IClock _clock;
    private readonly StudyReadService _read;

    public SubjectService(StudyDataContext db, AuditWriter audit, IClock clock, StudyReadService read)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
        _read = read;
    }

    public async Task<Subject> ScreenAsync(ScreenRequest r, CancellationToken ct = default)
    {
        var site = await _db.Sites.FirstOrDefaultAsync(s => s.Id == r.SiteId, ct)
            ?? throw new SubjectRuleException("Unknown site.");
        if (!site.Active)
        {
            throw new SubjectRuleException($"Site {site.Number} is not active.");
        }

        var study = await _read.GetDesignAsync(site.StudyId, ct) ?? throw new SubjectRuleException("Unknown study.");
        if (study.Status is not (StudyStatus.Enrolling))
        {
            throw new SubjectRuleException($"Study {study.Code} is not enrolling ({study.Status}).");
        }

        if (r.ConsentDate > DateOnly.FromDateTime(_clock.UtcNow).AddDays(1))
        {
            throw new SubjectRuleException("Consent date is in the future.");
        }

        var sequence = await _db.Subjects.CountAsync(s => s.SiteId == site.Id, ct) + 1;
        var subject = new Subject
        {
            StudyId = study.Id,
            SiteId = site.Id,
            Number = SubjectNumbers.Format(study.SubjectNumberFormat, site.Number, sequence),
            Initials = string.IsNullOrWhiteSpace(r.Initials) ? null : r.Initials.Trim().ToUpperInvariant(),
            BirthYear = r.BirthYear,
            Sex = r.Sex,
            ConsentDate = r.ConsentDate,
            Status = SubjectStatus.Screening
        };

        foreach (var def in study.Visits.OrderBy(v => v.Order))
        {
            var visit = new Visit { Definition = def, VisitDefinitionId = def.Id, Status = VisitStatus.Planned };
            foreach (var formCode in def.FormCodeList)
            {
                var form = study.Forms.First(f => f.Code == formCode);
                visit.Forms.Add(new FormInstance { FormDefinitionId = form.Id, Status = FormStatus.NotStarted, RowVersion = NewVersion() });
            }

            subject.Visits.Add(visit);
        }

        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync(ct);
        _audit.Add(r.User, "Subject", subject.Number, subject.Id, "Screened", newValue: $"site {site.Number}, consent {r.ConsentDate:yyyy-MM-dd}");
        _db.Notifications.Add(new Core.Documents.Notification
        {
            At = _clock.UtcNow, StudyId = study.Id, SiteId = site.Id, SubjectId = subject.Id, Kind = "SubjectScreened",
            Text = $"{subject.Number} screened at site {site.Number}"
        });
        await _db.SaveChangesAsync(ct);
        return subject;
    }

    public async Task<Visit> SetVisitDateAsync(int visitId, DateOnly date, string user, string? reason, CancellationToken ct = default)
    {
        var visit = await _db.Visits.Include(v => v.Definition).FirstOrDefaultAsync(v => v.Id == visitId, ct)
            ?? throw new SubjectRuleException("Unknown visit.");
        var subject = await _read.GetCasebookAsync(visit.SubjectId, ct) ?? throw new SubjectRuleException("Unknown subject.");
        var study = await _read.GetDesignAsync(subject.StudyId, ct) ?? throw new SubjectRuleException("Unknown study.");

        if (date > DateOnly.FromDateTime(_clock.UtcNow).AddDays(1))
        {
            throw new SubjectRuleException("Visit date is in the future.");
        }

        if (subject.ConsentDate is DateOnly consent && date < consent)
        {
            throw new SubjectRuleException("Visit date is before informed consent.");
        }

        if (visit.Date is not null && visit.Date != date && string.IsNullOrWhiteSpace(reason))
        {
            throw new SubjectRuleException("A reason for change is required.");
        }

        var old = visit.Date;
        visit.Date = date;
        if (visit.Status == VisitStatus.Planned)
        {
            visit.Status = VisitStatus.Started;
        }

        var anchor = visit.Definition!.IsAnchor ? date : VisitSchedule.AnchorDate(subject);
        foreach (var planned in VisitSchedule.Plan(study, anchor))
        {
            var v = subject.Visits.FirstOrDefault(x => x.VisitDefinitionId == planned.Definition.Id);
            if (v?.Date is DateOnly d)
            {
                v.OutOfWindow = !VisitSchedule.InWindow(planned, v.Id == visit.Id ? date : d);
            }
        }

        _audit.Add(user, "Visit", $"{subject.Number}/{visit.Definition.Code}", subject.Id, "VisitDate",
            old?.ToString("yyyy-MM-dd"), date.ToString("yyyy-MM-dd"), reason);
        await _db.SaveChangesAsync(ct);
        return visit;
    }

    public async Task<FormInstance> AddRowAsync(int visitId, string formCode, string user, CancellationToken ct = default)
    {
        var visit = await _db.Visits.Include(v => v.Forms).Include(v => v.Subject).FirstOrDefaultAsync(v => v.Id == visitId, ct)
            ?? throw new SubjectRuleException("Unknown visit.");
        var form = await _db.FormDefinitions.FirstOrDefaultAsync(f => f.StudyId == visit.Subject!.StudyId && f.Code == formCode, ct)
            ?? throw new SubjectRuleException("Unknown form.");
        if (!form.Repeating)
        {
            throw new SubjectRuleException($"{form.Name} does not have rows.");
        }

        var row = visit.Forms.Where(f => f.FormDefinitionId == form.Id).Select(f => f.Row).DefaultIfEmpty(0).Max() + 1;
        var instance = new FormInstance { VisitId = visit.Id, FormDefinitionId = form.Id, Row = row, Status = FormStatus.NotStarted, RowVersion = NewVersion() };
        _db.Forms.Add(instance);
        _audit.Add(user, "Form", $"{visit.Subject!.Number}/{form.Code}#{row}", visit.SubjectId, "RowAdded");
        await _db.SaveChangesAsync(ct);
        return instance;
    }

    public async Task<Subject> RandomiseAsync(int subjectId, string stratum, string user, CancellationToken ct = default)
    {
        var subject = await _read.GetCasebookAsync(subjectId, ct) ?? throw new SubjectRuleException("Unknown subject.");
        var study = await _read.GetDesignAsync(subject.StudyId, ct) ?? throw new SubjectRuleException("Unknown study.");
        if (subject.Status != SubjectStatus.Screening)
        {
            throw new SubjectRuleException($"{subject.Number} is {subject.Status}.");
        }

        var eligibility = study.Forms.FirstOrDefault(f => f.Code == "IE");
        if (eligibility is not null)
        {
            var ie = subject.Visits.SelectMany(v => v.Forms).FirstOrDefault(f => f.FormDefinitionId == eligibility.Id);
            var eligible = ie?.Values.FirstOrDefault(v => v.FieldCode == "IEYN")?.Value;
            if (ie is null || ie.Status < FormStatus.Complete || eligible != "1")
            {
                throw new SubjectRuleException("Eligibility must be complete and confirmed before randomisation.");
            }
        }

        var site = subject.Site!;
        var slot = await NextSlotAsync(study, site.Number, stratum, ct);
        var arm = study.Arms.First(a => a.Code == slot.ArmCode);

        slot.SubjectId = subject.Id;
        slot.UsedAt = _clock.UtcNow;
        subject.ArmId = arm.Id;
        subject.RandomisationNumber = slot.RandomisationNumber;
        subject.RandomisedAt = _clock.UtcNow;
        subject.Status = SubjectStatus.Randomised;

        // The arm itself is only written to the audit trail for open-label studies.
        _audit.Add(user, "Subject", subject.Number, subject.Id, "Randomised",
            newValue: study.Blinded ? slot.RandomisationNumber : $"{slot.RandomisationNumber} {arm.Code}", reason: $"stratum {stratum}");
        await _db.SaveChangesAsync(ct);
        return subject;
    }

    private async Task<RandomisationSlot> NextSlotAsync(Study study, string siteNumber, string stratum, CancellationToken ct)
    {
        var slot = await _db.RandomisationSlots
            .Where(s => s.StudyId == study.Id && s.SiteNumber == siteNumber && s.Stratum == stratum && s.SubjectId == null)
            .OrderBy(s => s.Sequence)
            .FirstOrDefaultAsync(ct);
        if (slot is not null)
        {
            return slot;
        }

        var used = await _db.RandomisationSlots.CountAsync(s => s.StudyId == study.Id && s.SiteNumber == siteNumber && s.Stratum == stratum, ct);
        var list = BlockRandomiser.Generate(study.Arms.OrderBy(a => a.Code).ToList(), 10,
            BlockRandomiser.Seed(study.Code, siteNumber, stratum) + used);
        var slots = list.Select((arm, i) => new RandomisationSlot
        {
            StudyId = study.Id,
            SiteNumber = siteNumber,
            Stratum = stratum,
            Sequence = used + i + 1,
            ArmCode = arm,
            RandomisationNumber = $"R{siteNumber}{stratum[..Math.Min(1, stratum.Length)]}{used + i + 1:0000}"
        }).ToList();
        _db.RandomisationSlots.AddRange(slots);
        await _db.SaveChangesAsync(ct);
        return slots[0];
    }

    public async Task WithdrawAsync(int subjectId, string reason, string user, CancellationToken ct = default)
    {
        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId, ct) ?? throw new SubjectRuleException("Unknown subject.");
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new SubjectRuleException("A withdrawal reason is required.");
        }

        var old = subject.Status;
        subject.Status = old == SubjectStatus.Screening ? SubjectStatus.ScreenFailed : SubjectStatus.Withdrawn;
        subject.WithdrawalReason = reason.Trim();
        _audit.Add(user, "Subject", subject.Number, subject.Id, "Status", old.ToString(), subject.Status.ToString(), reason);
        await _db.SaveChangesAsync(ct);
    }

    internal static byte[] NewVersion() => Guid.NewGuid().ToByteArray();
}
