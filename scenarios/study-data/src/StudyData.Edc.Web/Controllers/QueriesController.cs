using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyData.Core.Queries;
using StudyData.Data;
using StudyData.Data.Services;
using StudyData.Edc.Web.Services;

namespace StudyData.Edc.Web.Controllers;

[Authorize]
[Route("queries")]
public class QueriesController : Controller
{
    private readonly StudyDataContext _db;
    private readonly SiteAccess _access;
    private readonly QueryService _queries;

    public QueriesController(StudyDataContext db, SiteAccess access, QueryService queries)
    {
        _db = db;
        _access = access;
        _queries = queries;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(QueryState? state = QueryState.Open)
    {
        var ids = _access.SiteIds;
        var studies = await _db.Sites.Where(s => ids.Contains(s.Id)).Select(s => s.StudyId).Distinct().ToListAsync();
        var rows = new List<QueryRow>();
        foreach (var study in studies)
        {
            rows.AddRange(await _queries.ListAsync(study, ids, state));
        }

        ViewBag.State = state;
        return View(rows);
    }

    [HttpPost("{id:int}/answer")]
    public async Task<IActionResult> Answer(int id, string text, string? returnUrl)
    {
        var q = await _queries.GetAsync(id);
        if (q is null || await _access.SubjectAsync(q.SubjectId) is null)
        {
            return NotFound();
        }

        try
        {
            await _queries.AnswerAsync(id, text, _access.UserName);
            TempData["Message"] = "Answer sent to the monitor.";
        }
        catch (QueryTransitionException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return LocalRedirect(returnUrl ?? "/queries");
    }
}
