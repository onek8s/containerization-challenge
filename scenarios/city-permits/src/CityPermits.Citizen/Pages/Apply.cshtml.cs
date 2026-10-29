using CityPermits.Citizen.Services;
using CityPermits.Domain.Fees;
using CityPermits.Domain.Model;
using CityPermits.Domain.Rules;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CityPermits.Citizen.Pages;

public class ApplyModel : PageModel
{
    private readonly CitizenUser _user;
    private readonly CaseService _cases;

    public ApplyModel(CitizenUser user, CaseService cases) { _user = user; _cases = cases; }

    [BindProperty(SupportsGet = true)] public PermitKind Kind { get; set; } = PermitKind.BuildingPermit;
    [BindProperty] public string? Uprn { get; set; }
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public BuildingDetails Building { get; set; } = new() { Storeys = 1 };
    [BindProperty] public BusinessDetails Business { get; set; } = new();
    [BindProperty] public ParkingDetails Parking { get; set; } = new() { DurationMonths = 12, EmissionBand = VehicleEmissionBand.Standard };

    public string KindName => Kind switch { PermitKind.BuildingPermit => "building permit", PermitKind.BusinessLicence => "business licence", _ => "parking permit" };

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        ModelState.Clear();
        var applicant = await _user.CurrentAsync(User);
        var c = new PermitCase { Kind = Kind, Description = Description };
        switch (Kind)
        {
            case PermitKind.BuildingPermit:
                c.Building = Building;
                c.Title = $"{Building.WorkType}: {Description?.Split('.').FirstOrDefault()}".Trim(' ', ':');
                break;
            case PermitKind.BusinessLicence:
                c.Business = Business;
                c.Title = $"{FeeCalculator.Display(Business.Category)} licence: {Business.TradingName}";
                break;
            default:
                Parking.Registration = Parking.Registration?.ToUpperInvariant();
                c.Parking = Parking;
                c.Title = $"{Parking.PermitType} parking permit";
                break;
        }
        c.Uprn = string.IsNullOrEmpty(Uprn) ? null : Uprn;
        var result = new PermitCaseValidator().Validate(c);
        if (string.IsNullOrEmpty(Uprn) && !(Kind == PermitKind.ParkingPermit && Parking.PermitType == ParkingPermitType.Carer))
            ModelState.AddModelError(nameof(Uprn), "Select an address from the list.");
        foreach (var e in result.Errors) ModelState.AddModelError(e.PropertyName, e.ErrorMessage);
        if (!ModelState.IsValid) return Page();

        c = await _cases.CreateDraftAsync(applicant, c, Uprn);
        return RedirectToPage("/Cases/Details", new { id = c.Id });
    }
}
