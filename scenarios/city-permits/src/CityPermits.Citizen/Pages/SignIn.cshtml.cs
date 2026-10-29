using System.Security.Claims;
using CityPermits.Citizen.Services;
using CityPermits.Data;
using CityPermits.Domain.Model;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CityPermits.Citizen.Pages;

public class SignInModel : PageModel
{
    private readonly PermitsDbContext _db;
    private readonly IConfiguration _cfg;

    public SignInModel(PermitsDbContext db, IConfiguration cfg) { _db = db; _cfg = cfg; }

    public bool DevelopmentMode => _cfg["Eid:Mode"] == "Development";
    public List<Applicant> TestIdentities { get; private set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public async Task OnGetAsync()
    {
        if (DevelopmentMode) TestIdentities = await _db.Applicants.Where(a => a.EidSubject != null).OrderBy(a => a.DisplayName).Take(30).ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync(string subject)
    {
        if (!DevelopmentMode) return NotFound();
        var key = subject.Split('|')[0];
        var a = await _db.Applicants.FirstOrDefaultAsync(x => x.EidSubject == subject);
        var claims = new List<Claim> { new(CitizenUser.SubjectClaim, key), new(CitizenUser.NameClaim, a?.DisplayName ?? "Test Person") };
        if (a?.Type == ApplicantType.Organisation)
        {
            claims[0] = new(CitizenUser.SubjectClaim, subject);
            claims.Add(new(CitizenUser.OrganisationNameClaim, a.DisplayName));
        }
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, "Development", CitizenUser.NameClaim, ClaimTypes.Role)));
        return LocalRedirect(string.IsNullOrEmpty(ReturnUrl) ? "/Cases" : ReturnUrl);
    }
}
