using CityPermits.Archive;
using CityPermits.BackOffice.Services;
using CityPermits.Data;
using CityPermits.Domain.Model;
using CityPermits.Domain.Rules;
using CityPermits.Domain.Workflow;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CityPermits.BackOffice.Controllers;

public class CasesController : Controller
{
    private readonly PermitsDbContext _db;
    private readonly CaseActions _actions;
    private readonly IDocumentArchive _archive;

    public CasesController(PermitsDbContext db, CaseActions actions, IDocumentArchive archive) { _db = db; _actions = actions; _archive = archive; }

    private string Actor => User.Identity?.Name ?? "unknown";
    private IEnumerable<string> UserRoles => User.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value);

    public async Task<IActionResult> Index(string? team, string? status, bool mine = false, string? q = null)
    {
        var query = _db.Cases.Include(c => c.Applicant).AsQueryable();
        if (!string.IsNullOrEmpty(team)) query = query.Where(c => c.Team == team);
        if (Enum.TryParse<CaseStatus>(status, out var s)) query = query.Where(c => c.Status == s);
        else query = query.Where(c => c.Status >= CaseStatus.Submitted && c.Status <= CaseStatus.DecisionPending);
        if (mine) query = query.Where(c => c.AssignedOfficer == Actor);
        if (!string.IsNullOrEmpty(q)) query = query.Where(c => c.Reference.Contains(q) || c.Title.Contains(q) || (c.SiteAddress ?? "").Contains(q));
        ViewBag.Filter = new { team, status, mine, q };
        return View(await query.OrderBy(c => c.TargetDecisionDate).Take(200).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var c = await _actions.LoadAsync(id);
        if (c is null) return NotFound();
        ViewBag.Site = CaseActions.Site(c);
        ViewBag.Findings = ViewBag.Site is null ? Array.Empty<RuleFinding>() : PlanningRules.Evaluate(c, ViewBag.Site);
        ViewBag.Actions = CaseWorkflow.Available(c, UserRoles).ToList();
        ViewBag.Officers = await _db.Cases.Where(x => x.AssignedOfficer != null).Select(x => x.AssignedOfficer!).Distinct().ToListAsync();
        ViewBag.StandardConditions = await _db.StandardConditions.Where(x => x.AppliesTo == c.Kind.ToString()).ToListAsync();
        return View(c);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Act(int id, string action, string? note)
    {
        var c = await _actions.LoadAsync(id);
        if (c is null) return NotFound();
        try
        {
            if (action == CaseWorkflow.StartIntake && c.AssignedOfficer is null) c.AssignedOfficer = Actor;
            CaseWorkflow.Apply(c, action, Actor, UserRoles, note, DateTime.UtcNow);
            if (c.Status is CaseStatus.Approved) await _actions.IssueAsync(c, Actor);
            else if (c.Status is CaseStatus.Refused) await _actions.AddDecisionDocumentsAsync(c, Actor);
            await _db.SaveChangesAsync();
            TempData["Message"] = $"{action} done.";
        }
        catch (WorkflowException ex) { TempData["Error"] = ex.Message; }
        catch (DbUpdateConcurrencyException) { TempData["Error"] = "The case was changed by someone else. Reload and try again."; }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SiteChecks(int id)
    {
        var c = await _actions.LoadAsync(id);
        if (c is null) return NotFound();
        var findings = _actions.RunSiteChecks(c, Actor);
        await _db.SaveChangesAsync();
        TempData["Message"] = $"Site checks done: {findings.Count} findings.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int id, string officer)
    {
        var c = await _db.Cases.Include(x => x.Events).FirstAsync(x => x.Id == id);
        c.AssignedOfficer = officer;
        c.Events.Add(new CaseEvent { AtUtc = DateTime.UtcNow, Actor = Actor, Type = "assign", Text = officer });
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCondition(int id, string code, string? text)
    {
        var c = await _db.Cases.Include(x => x.Conditions).FirstAsync(x => x.Id == id);
        var std = await _db.StandardConditions.FindAsync(code);
        c.Conditions.Add(new Condition { Code = code, Text = text ?? std?.Text ?? "", Order = c.Conditions.Count + 1 });
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordConsultation(int id, int consultationId, string response, bool objection)
    {
        var x = await _db.Consultations.FirstAsync(k => k.Id == consultationId && k.CaseId == id);
        x.Response = response; x.Objection = objection; x.RespondedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Microsoft.AspNetCore.Authorization.Authorize(Policy = "Finance")]
    public async Task<IActionResult> Waive(int id, int feeId, string reason)
    {
        var f = await _db.FeeLines.FirstAsync(x => x.Id == feeId && x.CaseId == id);
        f.Waived = true; f.WaivedBy = Actor; f.WaiverReason = reason;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Document(int id)
    {
        var d = await _db.CaseDocuments.FindAsync(id);
        return d is null ? NotFound() : File(await _archive.OpenReadAsync(d.ArchiveKey), d.ContentType, d.FileName);
    }

    public async Task<IActionResult> Search(string q) => View("SearchResults", await _archive.SearchAsync(q));

    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public IActionResult Error() => Content("An error occurred. The service desk has been notified.");
}
