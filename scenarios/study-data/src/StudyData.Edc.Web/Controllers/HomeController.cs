using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyData.Core.Queries;
using StudyData.Data;
using StudyData.Edc.Web.Models;
using StudyData.Edc.Web.Services;

namespace StudyData.Edc.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly StudyDataContext _db;
    private readonly SiteAccess _access;

    public HomeController(StudyDataContext db, SiteAccess access)
    {
        _db = db;
        _access = access;
    }

    public async Task<IActionResult> Index()
    {
        var ids = _access.SiteIds;
        var sites = await _db.Sites.Include(s => s.Study).Where(s => ids.Contains(s.Id)).ToListAsync();
        var subjects = await _db.Subjects.Where(s => ids.Contains(s.SiteId)).GroupBy(s => s.SiteId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        var queries = await (from q in _db.Queries
                             join s in _db.Subjects on q.SubjectId equals s.Id
                             where ids.Contains(s.SiteId) && (q.State == QueryState.Open || q.State == QueryState.Answered)
                             group q by new { s.SiteId, q.State } into g
                             select new { g.Key.SiteId, g.Key.State, Count = g.Count() }).ToListAsync();

        var model = sites.Select(s => new SiteSummary(s.Id, s.Study!.Code, s.Study.Title, s.Number, s.Name,
            subjects.FirstOrDefault(x => x.Key == s.Id)?.Count ?? 0,
            queries.Where(q => q.SiteId == s.Id && q.State == QueryState.Open).Sum(q => q.Count),
            queries.Where(q => q.SiteId == s.Id && q.State == QueryState.Answered).Sum(q => q.Count))).ToList();
        return View(model);
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}
