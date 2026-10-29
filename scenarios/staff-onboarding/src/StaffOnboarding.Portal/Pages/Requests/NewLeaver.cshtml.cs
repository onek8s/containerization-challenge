using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StaffOnboarding.Core.Requests;
using StaffOnboarding.Portal.Data;
using StaffOnboarding.Portal.Services;

namespace StaffOnboarding.Portal.Pages.Requests;

public class NewLeaverModel(OnboardingRepository repository, WorkflowGateway workflows, CurrentUser user, ILogger<NewLeaverModel> logger) : PageModel
{
    public sealed class LeaverForm
    {
        [Required, RegularExpression(@"^[A-Za-z0-9._-]{2,20}$")] public string SamAccountName { get; set; } = "";
        [DataType(DataType.Date)] public DateTime LastWorkingDay { get; set; } = DateTime.Today.AddDays(14);
        public bool Immediate { get; set; }
        [RegularExpression(@"^[A-Za-z0-9._-]{2,20}$")] public string? HandoverTo { get; set; }
        [StringLength(2000)] public string? Notes { get; set; }
    }

    [BindProperty] public LeaverForm Form { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!Form.Immediate && Form.LastWorkingDay.Date < DateTime.Today)
            ModelState.AddModelError("Form.LastWorkingDay", "The last working day is in the past. Choose immediate disabling instead.");
        if (!ModelState.IsValid) return Page();

        var request = new LeaverRequest
        {
            RequestedBy = user.Name,
            SamAccountName = Form.SamAccountName.Trim().ToLowerInvariant(),
            LastWorkingDay = Form.LastWorkingDay.Date,
            Immediate = Form.Immediate,
            HandoverTo = Form.HandoverTo?.Trim().ToLowerInvariant(),
            Notes = Form.Notes
        };

        request.RequestNumber = await repository.CreateRequestAsync(RequestKind.Leaver, request.SamAccountName, request.SamAccountName,
            null, null, request.LastWorkingDay, user.Name, JsonSerializer.Serialize(request));

        return await NewRequest.StartAsync(this, repository, logger, request.RequestNumber, () => workflows.SubmitAsync(request));
    }
}
