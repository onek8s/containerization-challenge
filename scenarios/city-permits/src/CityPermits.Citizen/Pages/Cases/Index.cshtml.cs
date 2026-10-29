using CityPermits.Citizen.Services;
using CityPermits.Data;
using CityPermits.Domain.Model;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CityPermits.Citizen.Pages.Cases;

public class IndexModel : PageModel
{
    private readonly PermitsDbContext _db;
    private readonly CitizenUser _user;

    public IndexModel(PermitsDbContext db, CitizenUser user) { _db = db; _user = user; }

    public Applicant? Applicant { get; private set; }
    public List<PermitCase> Items { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Applicant = await _user.CurrentAsync(User);
        Items = await _db.Cases.Where(c => c.ApplicantId == Applicant.Id).OrderByDescending(c => c.CreatedUtc).ToListAsync();
    }
}
