using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StaffOnboarding.Core.Requests;
using StaffOnboarding.Portal.Data;
using StaffOnboarding.Portal.Services;

namespace StaffOnboarding.Portal.Pages.Tasks;

public class DetailsModel(OnboardingRepository repository, WorkflowGateway workflows, CurrentUser user) : PageModel
{
    public TaskRow? Task { get; private set; }
    public string Summary { get; private set; } = "";
    public bool CanDecide { get; private set; }
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id) => await LoadAsync(id) ? Page() : NotFound();

    public async Task<IActionResult> OnPostAsync(Guid id, DecisionOutcome outcome, string? comment)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (!CanDecide || Task!.Status != "Open") return Forbid();

        if (outcome == DecisionOutcome.Rejected && string.IsNullOrWhiteSpace(comment))
        {
            Error = "Please give a reason for the rejection.";
            return Page();
        }

        try
        {
            await workflows.DecideAsync(Task.RequestKind, new ApprovalDecision
            {
                RequestNumber = Task.RequestNumber,
                TaskId = Task.TaskId,
                Outcome = outcome,
                Comment = comment,
                DecidedBy = user.Account
            });
        }
        catch (WorkflowRejectedException ex)
        {
            Error = "The request is no longer waiting for this decision. " + ex.Message;
            return Page();
        }

        TempData["Message"] = $"Your decision on {Task.RequestNumber} was recorded.";
        return RedirectToPage("/Index");
    }

    private async Task<bool> LoadAsync(Guid id)
    {
        Task = await repository.GetTaskAsync(id);
        if (Task == null) return false;
        CanDecide = user.IsAssignee(Task.AssignedTo);

        var request = await repository.GetRequestAsync(Task.RequestNumber);
        if (request != null)
        {
            using var doc = JsonDocument.Parse(request.Payload);
            Summary = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        return true;
    }
}
