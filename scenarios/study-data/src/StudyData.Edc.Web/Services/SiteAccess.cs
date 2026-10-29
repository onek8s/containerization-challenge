using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StudyData.Core.Subjects;
using StudyData.Data;

namespace StudyData.Edc.Web.Services;

/// <summary>Site staff see only the subjects of the sites they are delegated to.</summary>
public class SiteAccess
{
    private readonly IHttpContextAccessor _http;
    private readonly StudyDataContext _db;

    public SiteAccess(IHttpContextAccessor http, StudyDataContext db)
    {
        _http = http;
        _db = db;
    }

    private ClaimsPrincipal User => _http.HttpContext!.User;

    public string UserName => User.Identity?.Name ?? "unknown";

    public string DisplayName => User.FindFirstValue("name") ?? UserName;

    public IReadOnlyCollection<int> SiteIds =>
        User.FindAll(SiteClaimsFactory.SiteClaim).Select(c => int.Parse(c.Value)).ToHashSet();

    public bool CanSign(int siteId) => User.FindAll(SiteClaimsFactory.SignSiteClaim).Any(c => c.Value == siteId.ToString());

    public bool HasSite(int siteId) => SiteIds.Contains(siteId);

    public async Task<Subject?> SubjectAsync(int subjectId)
    {
        var subject = await _db.Subjects.Include(s => s.Site).FirstOrDefaultAsync(s => s.Id == subjectId);
        return subject is not null && HasSite(subject.SiteId) ? subject : null;
    }
}
