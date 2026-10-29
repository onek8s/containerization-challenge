using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyData.Core.Services;
using StudyData.Data;
using StudyData.Data.Services;
using StudyData.Edc.Web.Models;
using StudyData.Edc.Web.Services;

namespace StudyData.Edc.Web.Controllers;

[Authorize]
[Route("forms")]
public class FormsController : Controller
{
    private readonly StudyDataContext _db;
    private readonly SiteAccess _access;
    private readonly FormEntryService _entry;
    private readonly StudyReadService _read;
    private readonly ILogger<FormsController> _log;

    public FormsController(StudyDataContext db, SiteAccess access, FormEntryService entry, StudyReadService read, ILogger<FormsController> log)
    {
        _db = db;
        _access = access;
        _entry = entry;
        _read = read;
        _log = log;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var model = await LoadAsync(id);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost("{id:int}")]
    public async Task<IActionResult> Edit(int id, [FromForm] Dictionary<string, string?> values, string? reason, string rowVersion)
    {
        var model = await LoadAsync(id);
        if (model is null)
        {
            return NotFound();
        }

        var fieldValues = model.Definition.Fields.ToDictionary(f => f.Code, f => values.GetValueOrDefault("v_" + f.Code));
        try
        {
            var result = await _entry.SaveAsync(new SaveFormRequest(id, fieldValues, _access.UserName, reason, Convert.FromBase64String(rowVersion)));
            if (result.Saved && result.Queries.Count == 0 && result.Warnings.Count == 0)
            {
                TempData["Message"] = $"{model.Definition.Name} saved.";
                return RedirectToAction("Details", "Subjects", new { id = model.Subject.Id });
            }

            var reloaded = (await LoadAsync(id))!;
            reloaded.Result = result;
            if (!result.Saved)
            {
                reloaded.Values = new Dictionary<string, string?>(fieldValues, StringComparer.OrdinalIgnoreCase);
                reloaded.Reason = reason;
                reloaded.RowVersion = rowVersion;
            }

            return View(reloaded);
        }
        catch (FormConflictException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Edit), new { id });
        }
        catch (DerivationException ex)
        {
            _log.LogError(ex, "Derivation failed for form {Form}", id);
            model.Values = new Dictionary<string, string?>(fieldValues, StringComparer.OrdinalIgnoreCase);
            model.Reason = reason;
            ModelState.AddModelError("", $"The form could not be calculated and was not saved: {ex.Message}");
            return View(model);
        }
        catch (SubjectRuleException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Edit), new { id });
        }
    }

    private async Task<FormEditModel?> LoadAsync(int id)
    {
        var form = await _db.Forms.Include(f => f.Values).Include(f => f.Visit).ThenInclude(v => v!.Definition).FirstOrDefaultAsync(f => f.Id == id);
        if (form is null)
        {
            return null;
        }

        var subject = await _access.SubjectAsync(form.Visit!.SubjectId);
        if (subject is null)
        {
            return null;
        }

        var study = (await _read.GetDesignAsync(subject.StudyId))!;
        return new FormEditModel
        {
            Subject = subject,
            Visit = form.Visit,
            Definition = study.Forms.First(d => d.Id == form.FormDefinitionId),
            Form = form,
            Values = form.Values.ToDictionary(v => v.FieldCode, v => v.Value, StringComparer.OrdinalIgnoreCase),
            Queries = await _db.Queries.Include(q => q.Messages).Where(q => q.FormInstanceId == id).OrderByDescending(q => q.OpenedAt).ToListAsync(),
            RowVersion = Convert.ToBase64String(form.RowVersion)
        };
    }
}
