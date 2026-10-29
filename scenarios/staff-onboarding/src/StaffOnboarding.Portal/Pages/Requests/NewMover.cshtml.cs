using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using StaffOnboarding.Core.Requests;
using StaffOnboarding.Portal.Data;
using StaffOnboarding.Portal.Services;

namespace StaffOnboarding.Portal.Pages.Requests;

public class NewMoverModel(OnboardingRepository repository, WorkflowGateway workflows, ReferenceData reference,
    CurrentUser user, ILogger<NewMoverModel> logger) : PageModel
{
    public sealed class MoverForm
    {
        [Required, RegularExpression(@"^[A-Za-z0-9._-]{2,20}$")] public string SamAccountName { get; set; } = "";
        [Required] public string Department { get; set; } = "";
        [Required] public string JobRole { get; set; } = "";
        [Required, StringLength(80)] public string Title { get; set; } = "";
        [StringLength(60)] public string? Office { get; set; }
        [StringLength(20)] public string? CostCenter { get; set; }
        [Required, RegularExpression(@"^[A-Za-z0-9._-]{2,20}$")] public string ManagerAccount { get; set; } = "";
        public EmploymentType EmploymentType { get; set; }
        [DataType(DataType.Date)] public DateTime? ContractEndDate { get; set; }
        [DataType(DataType.Date)] public DateTime EffectiveDate { get; set; } = DateTime.Today.AddDays(7);
        [Range(0, 30)] public int HandoverDays { get; set; } = 10;
        public List<string> AdditionalGroups { get; set; } = new();
        [StringLength(2000)] public string? Notes { get; set; }
    }

    [BindProperty] public MoverForm Form { get; set; } = new();

    public IEnumerable<SelectListItem> Departments => reference.AccessMap.Departments.Select(d => new SelectListItem(d.Name, d.Code));
    public IEnumerable<SelectListItem> Roles => reference.AccessMap.Departments
        .SelectMany(d => d.Roles.Values.Select(r => new SelectListItem($"{d.Code}: {r.Title}", r.Code)));
    public IEnumerable<SelectListItem> Requestable => reference.RequestableGroups.Select(g => new SelectListItem(g, g));

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var department = reference.AccessMap.FindDepartment(Form.Department);
        if (department != null && department.FindRole(Form.JobRole) == null)
            ModelState.AddModelError("Form.JobRole", "The job role does not belong to the department.");
        if (Form.EffectiveDate.Date < DateTime.Today.AddDays(1))
            ModelState.AddModelError("Form.EffectiveDate", "The effective date must be in the future.");
        if (!ModelState.IsValid) return Page();

        var request = new MoverRequest
        {
            RequestedBy = user.Name,
            SamAccountName = Form.SamAccountName.Trim().ToLowerInvariant(),
            NewPosition = new PositionDetails
            {
                Department = Form.Department, JobRole = Form.JobRole, Title = Form.Title, Office = Form.Office,
                CostCenter = Form.CostCenter, ManagerAccount = Form.ManagerAccount.Trim().ToLowerInvariant(),
                EmploymentType = Form.EmploymentType, ContractEndDate = Form.ContractEndDate
            },
            EffectiveDate = Form.EffectiveDate.Date,
            HandoverDays = Form.HandoverDays,
            AdditionalGroups = Form.AdditionalGroups,
            Notes = Form.Notes
        };

        request.RequestNumber = await repository.CreateRequestAsync(RequestKind.Mover, request.SamAccountName, request.SamAccountName,
            request.NewPosition.Department, request.NewPosition.ManagerAccount, request.EffectiveDate, user.Name, JsonSerializer.Serialize(request));

        return await NewRequest.StartAsync(this, repository, logger, request.RequestNumber, () => workflows.SubmitAsync(request));
    }
}
