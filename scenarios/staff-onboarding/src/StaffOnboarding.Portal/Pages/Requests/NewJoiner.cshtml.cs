using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using StaffOnboarding.Core.Requests;
using StaffOnboarding.Core.Scheduling;
using StaffOnboarding.Portal.Data;
using StaffOnboarding.Portal.Services;

namespace StaffOnboarding.Portal.Pages.Requests;

public class NewJoinerModel(OnboardingRepository repository, WorkflowGateway workflows, DocumentStore documents,
    ReferenceData reference, CurrentUser user, ILogger<NewJoinerModel> logger) : PageModel
{
    public sealed class JoinerForm
    {
        [Required, StringLength(60)] public string FirstName { get; set; } = "";
        [StringLength(40)] public string? PreferredName { get; set; }
        [Required, StringLength(60)] public string LastName { get; set; } = "";
        [StringLength(20)] public string? EmployeeId { get; set; }
        [Phone] public string? MobilePhone { get; set; }
        [Required] public string Department { get; set; } = "";
        [Required] public string JobRole { get; set; } = "";
        [Required, StringLength(80)] public string Title { get; set; } = "";
        [StringLength(60)] public string? Office { get; set; }
        [StringLength(20)] public string? CostCenter { get; set; }
        [Required, RegularExpression(@"^[A-Za-z0-9._-]{2,20}$")] public string ManagerAccount { get; set; } = "";
        public EmploymentType EmploymentType { get; set; }
        [DataType(DataType.Date)] public DateTime? ContractEndDate { get; set; }
        [DataType(DataType.Date)] public DateTime StartDate { get; set; } = DateTime.Today.AddDays(14);
        public List<string> AdditionalGroups { get; set; } = new();
        [StringLength(2000)] public string? Notes { get; set; }
    }

    [BindProperty] public JoinerForm Form { get; set; } = new();

    public DateTime EarliestStart => reference.EarliestStartDate(DateTime.Today);
    public int LeadDays => new ScheduleOptions().ProvisioningLeadBusinessDays;
    public IEnumerable<SelectListItem> Departments => reference.AccessMap.Departments.Select(d => new SelectListItem(d.Name, d.Code));
    public IEnumerable<SelectListItem> Roles => reference.AccessMap.Departments
        .SelectMany(d => d.Roles.Values.Select(r => new SelectListItem($"{d.Code}: {r.Title}", r.Code)));
    public IEnumerable<SelectListItem> Requestable => reference.RequestableGroups.Select(g => new SelectListItem(g, g));

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(IFormFile? contract)
    {
        var department = reference.AccessMap.FindDepartment(Form.Department);
        if (department != null && department.FindRole(Form.JobRole) == null)
            ModelState.AddModelError("Form.JobRole", "The job role does not belong to the department.");
        if (Form.StartDate.Date < EarliestStart)
            ModelState.AddModelError("Form.StartDate", $"The start date must be {EarliestStart:dd.MM.yyyy} or later.");
        if (Form.EmploymentType != EmploymentType.Permanent && Form.ContractEndDate == null)
            ModelState.AddModelError("Form.ContractEndDate", "A contract end date is required for this employment type.");
        if (Form.ContractEndDate != null && Form.ContractEndDate <= Form.StartDate)
            ModelState.AddModelError("Form.ContractEndDate", "The contract must end after the start date.");
        if (!ModelState.IsValid) return Page();

        var request = new JoinerRequest
        {
            RequestedBy = user.Name,
            Person = new PersonDetails
            {
                FirstName = Form.FirstName.Trim(), PreferredName = Form.PreferredName?.Trim(), LastName = Form.LastName.Trim(),
                EmployeeId = Form.EmployeeId, MobilePhone = Form.MobilePhone
            },
            Position = new PositionDetails
            {
                Department = Form.Department, JobRole = Form.JobRole, Title = Form.Title, Office = Form.Office,
                CostCenter = Form.CostCenter, ManagerAccount = Form.ManagerAccount.Trim().ToLowerInvariant(),
                EmploymentType = Form.EmploymentType, ContractEndDate = Form.ContractEndDate
            },
            StartDate = Form.StartDate.Date,
            AdditionalGroups = Form.AdditionalGroups,
            Notes = Form.Notes
        };

        request.RequestNumber = await repository.CreateRequestAsync(RequestKind.Joiner, request.Person.DisplayName, null,
            request.Position.Department, request.Position.ManagerAccount, request.StartDate, user.Name, JsonSerializer.Serialize(request));

        if (contract is { Length: > 0 })
            await documents.SaveAsync(request.RequestNumber, contract);

        return await NewRequest.StartAsync(this, repository, logger, request.RequestNumber, () => workflows.SubmitAsync(request));
    }
}

internal static class NewRequest
{
    /// <summary>Starts the workflow for a stored request; a failure is recorded on the request.</summary>
    public static async Task<IActionResult> StartAsync(PageModel page, OnboardingRepository repository, ILogger logger,
        string requestNumber, Func<Task<string>> submit)
    {
        try
        {
            await submit();
            page.TempData["Message"] = $"Request {requestNumber} submitted.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Starting workflow for {RequestNumber} failed", requestNumber);
            await repository.MarkStartFailedAsync(requestNumber, ex.Message);
            page.TempData["Message"] = $"Request {requestNumber} was saved but could not be started. IT has been notified.";
        }
        return page.RedirectToPage("/Requests/Details", new { number = requestNumber });
    }
}
