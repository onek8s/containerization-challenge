using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyData.Core.Queries;
using StudyData.Core.Scheduling;
using StudyData.Data;
using StudyData.Data.Services;
using StudyData.Edc.Web.Models;
using StudyData.Edc.Web.Services;

namespace StudyData.Edc.Web.Controllers;

[Authorize]
[Route("subjects")]
public class SubjectsController : Controller
{
    private readonly StudyDataContext _db;
    private readonly SiteAccess _access;
    private readonly SubjectService _subjects;
    private readonly StudyReadService _read;
    private readonly ReviewService _review;
    private readonly SignInManager<SiteUser> _signIn;
    private readonly UserManager<SiteUser> _users;

    public SubjectsController(StudyDataContext db, SiteAccess access, SubjectService subjects, StudyReadService read, ReviewService review,
        SignInManager<SiteUser> signIn, UserManager<SiteUser> users)
    {
        _db = db;
        _access = access;
        _subjects = subjects;
        _read = read;
        _review = review;
        _signIn = signIn;
        _users = users;
    }

    [HttpGet("site/{siteId:int}")]
    public async Task<IActionResult> Index(int siteId)
    {
        if (!_access.HasSite(siteId))
        {
            return Forbid();
        }

        ViewBag.Site = await _db.Sites.Include(s => s.Study).FirstAsync(s => s.Id == siteId);
        ViewBag.OpenQueries = await _db.Queries.Where(q => q.State == QueryState.Open)
            .GroupBy(q => q.SubjectId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var list = await _db.Subjects.Where(s => s.SiteId == siteId).OrderBy(s => s.Number).ToListAsync();
        return View(list);
    }

    [HttpGet("site/{siteId:int}/screen")]
    public async Task<IActionResult> Screen(int siteId)
    {
        if (!_access.HasSite(siteId))
        {
            return Forbid();
        }

        var site = await _db.Sites.FirstAsync(s => s.Id == siteId);
        return View(new ScreenModel { SiteId = siteId, SiteName = $"{site.Number} {site.Name}", BirthYear = 1970 });
    }

    [HttpPost("site/{siteId:int}/screen")]
    public async Task<IActionResult> Screen(int siteId, ScreenModel model)
    {
        if (!_access.HasSite(siteId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var subject = await _subjects.ScreenAsync(new ScreenRequest(siteId, model.Initials, model.BirthYear, model.Sex, model.ConsentDate, _access.UserName));
            TempData["Message"] = $"Subject {subject.Number} registered.";
            return RedirectToAction(nameof(Details), new { id = subject.Id });
        }
        catch (SubjectRuleException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(model);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        if (await _access.SubjectAsync(id) is null)
        {
            return NotFound();
        }

        var subject = (await _read.GetCasebookAsync(id))!;
        var study = (await _read.GetDesignAsync(subject.StudyId))!;
        var open = await _db.Queries.Where(q => q.SubjectId == id && q.State == QueryState.Open)
            .GroupBy(q => q.FormInstanceId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var plan = VisitSchedule.Plan(study, VisitSchedule.AnchorDate(subject));

        var model = new SubjectDetailsModel
        {
            Subject = subject,
            Study = study,
            OpenQueries = open.Values.Sum(),
            CanSign = _access.CanSign(subject.SiteId),
            Documents = await _db.SourceDocuments.Where(d => d.SubjectId == id).OrderByDescending(d => d.UploadedAt).ToListAsync()
        };
        foreach (var visit in subject.Visits.OrderBy(v => v.Definition!.Order).ThenBy(v => v.Repeat))
        {
            var p = plan.FirstOrDefault(x => x.Definition.Id == visit.VisitDefinitionId);
            model.Visits.Add(new VisitRow(visit, p?.Target, p?.Earliest, p?.Latest,
                visit.Forms.Select(f => (f, study.Forms.First(d => d.Id == f.FormDefinitionId), open.GetValueOrDefault(f.Id)))
                    .OrderBy(x => x.Item2.Order).ThenBy(x => x.f.Row).ToList()));
        }

        return View(model);
    }

    [HttpPost("{id:int}/visits/{visitId:int}/date")]
    public async Task<IActionResult> VisitDate(int id, int visitId, DateOnly date, string? reason)
    {
        if (await _access.SubjectAsync(id) is null)
        {
            return NotFound();
        }

        try
        {
            var visit = await _subjects.SetVisitDateAsync(visitId, date, _access.UserName, reason);
            TempData["Message"] = visit.OutOfWindow ? $"Visit date saved. The visit is outside its window." : "Visit date saved.";
        }
        catch (SubjectRuleException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:int}/visits/{visitId:int}/rows")]
    public async Task<IActionResult> AddRow(int id, int visitId, string form)
    {
        if (await _access.SubjectAsync(id) is null)
        {
            return NotFound();
        }

        var instance = await _subjects.AddRowAsync(visitId, form, _access.UserName);
        return RedirectToAction("Edit", "Forms", new { id = instance.Id });
    }

    [HttpPost("{id:int}/randomise")]
    public async Task<IActionResult> Randomise(int id, string stratum)
    {
        if (await _access.SubjectAsync(id) is null)
        {
            return NotFound();
        }

        try
        {
            var s = await _subjects.RandomiseAsync(id, stratum, _access.UserName);
            TempData["Message"] = $"{s.Number} randomised: randomisation number {s.RandomisationNumber}. Dispense the kit with this number.";
        }
        catch (SubjectRuleException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:int}/withdraw")]
    public async Task<IActionResult> Withdraw(int id, string reason)
    {
        if (await _access.SubjectAsync(id) is null)
        {
            return NotFound();
        }

        try
        {
            await _subjects.WithdrawAsync(id, reason, _access.UserName);
        }
        catch (SubjectRuleException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet("{id:int}/sign")]
    public async Task<IActionResult> Sign(int id)
    {
        var subject = await _access.SubjectAsync(id);
        if (subject is null || !_access.CanSign(subject.SiteId))
        {
            return Forbid();
        }

        var forms = await _db.Forms.CountAsync(f => f.Visit!.SubjectId == id && (f.Status == Core.Subjects.FormStatus.Complete || f.Status == Core.Subjects.FormStatus.Verified));
        return View(new SignModel { SubjectId = id, SubjectNumber = subject.Number, Forms = forms });
    }

    /// <summary>Electronic signature: the investigator re-enters the password (21 CFR 11.200).</summary>
    [HttpPost("{id:int}/sign")]
    public async Task<IActionResult> Sign(int id, SignModel model)
    {
        var subject = await _access.SubjectAsync(id);
        if (subject is null || !_access.CanSign(subject.SiteId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            model.SubjectNumber = subject.Number;
            return View(model);
        }

        var user = await _users.GetUserAsync(User);
        var check = await _signIn.CheckPasswordSignInAsync(user!, model.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            ModelState.AddModelError("", check.IsLockedOut ? "The account is locked." : "The password is not correct.");
            model.SubjectNumber = subject.Number;
            return View(model);
        }

        try
        {
            var count = await _review.SignCasebookAsync(id, $"{_access.DisplayName} <{_access.UserName}>",
                "I approve the data in this casebook as accurate and complete (investigator signature)");
            TempData["Message"] = $"{count} forms signed.";
        }
        catch (SubjectRuleException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
