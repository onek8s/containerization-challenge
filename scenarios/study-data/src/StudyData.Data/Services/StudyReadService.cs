using Microsoft.EntityFrameworkCore;
using StudyData.Core.Design;
using StudyData.Core.Subjects;

namespace StudyData.Data.Services;

public class StudyReadService
{
    private readonly StudyDataContext _db;

    public StudyReadService(StudyDataContext db)
    {
        _db = db;
    }

    public Task<Study?> GetDesignAsync(int studyId, CancellationToken ct = default) =>
        _db.Studies
            .Include(s => s.Sites)
            .Include(s => s.Arms)
            .Include(s => s.Visits)
            .Include(s => s.Forms).ThenInclude(f => f.Fields)
            .Include(s => s.Forms).ThenInclude(f => f.EditChecks)
            .Include(s => s.Workbooks).ThenInclude(w => w.Inputs)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == studyId, ct);

    public async Task<Study?> GetDesignAsync(string studyCode, CancellationToken ct = default)
    {
        var id = await _db.Studies.Where(s => s.Code == studyCode).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
        return id is null ? null : await GetDesignAsync(id.Value, ct);
    }

    /// <summary>Subject with all visits, forms and values (one casebook).</summary>
    public Task<Subject?> GetCasebookAsync(int subjectId, CancellationToken ct = default) =>
        _db.Subjects
            .Include(s => s.Site)
            .Include(s => s.Arm)
            .Include(s => s.Visits).ThenInclude(v => v.Definition)
            .Include(s => s.Visits).ThenInclude(v => v.Forms).ThenInclude(f => f.Values)
            .Include(s => s.Visits).ThenInclude(v => v.Forms).ThenInclude(f => f.Definition)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == subjectId, ct);
}
