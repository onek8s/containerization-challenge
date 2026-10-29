using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StaffOnboarding.Core.Requests;
using StaffOnboarding.Portal.Data;
using StaffOnboarding.Portal.Services;

namespace StaffOnboarding.Portal.Pages.Requests;

public class DetailsModel(OnboardingRepository repository, WorkflowGateway workflows, DocumentStore documents, CurrentUser user) : PageModel
{
    public RequestRow? Row { get; private set; }
    public AccountRow? Account { get; private set; }
    public IReadOnlyList<TaskRow> Tasks { get; private set; } = Array.Empty<TaskRow>();
    public IReadOnlyList<TimelineRow> Timeline { get; private set; } = Array.Empty<TimelineRow>();
    public IReadOnlyList<FileInfo> Documents { get; private set; } = Array.Empty<FileInfo>();
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(string number) => await LoadAsync(number) ? Page() : NotFound();

    public Task<IActionResult> OnPostCancelAsync(string number, string reason) =>
        SendAsync(number, r => workflows.CancelAsync(r.Kind, number, user.Account, reason), "Request cancelled.");

    public Task<IActionResult> OnPostStartDateAsync(string number, DateTime date) =>
        SendAsync(number, _ => workflows.ChangeStartDateAsync(number, date, user.Account), "Start date changed.");

    public Task<IActionResult> OnPostLastDayAsync(string number, DateTime date) =>
        SendAsync(number, _ => workflows.ChangeLastWorkingDayAsync(number, date, user.Account), "Last working day changed.");

    public Task<IActionResult> OnPostDisableNowAsync(string number, string reason) =>
        SendAsync(number, _ => workflows.DisableNowAsync(number, user.Account, reason), "The account will be disabled now.");

    public async Task<IActionResult> OnPostUploadAsync(string number, IFormFile? document)
    {
        if (!user.IsHr) return Forbid();
        if (!await LoadAsync(number)) return NotFound();
        if (document == null || document.Length == 0)
        {
            Error = "Choose a file to attach.";
            return Page();
        }
        try
        {
            var name = await documents.SaveAsync(number, document);
            await repository.AddTimelineAsync(number, user.Account, "Document attached", name);
        }
        catch (InvalidOperationException ex)
        {
            Error = ex.Message;
            return Page();
        }
        return RedirectToPage(new { number });
    }

    private async Task<IActionResult> SendAsync(string number, Func<RequestRow, Task> send, string done)
    {
        if (!user.IsHr) return Forbid();
        if (!await LoadAsync(number)) return NotFound();
        try
        {
            await send(Row!);
        }
        catch (WorkflowRejectedException ex)
        {
            Error = "The request cannot be changed at this stage. " + ex.Message;
            return Page();
        }
        TempData["Message"] = done;
        return RedirectToPage(new { number });
    }

    private async Task<bool> LoadAsync(string number)
    {
        Row = await repository.GetRequestAsync(number);
        if (Row == null) return false;
        Account = await repository.GetAccountAsync(number);
        Tasks = await repository.GetTasksForRequestAsync(number);
        Timeline = await repository.GetTimelineAsync(number);
        try
        {
            Documents = documents.List(number);
        }
        catch (IOException ex)
        {
            Error = "Documents are not available: " + ex.Message;
        }
        return true;
    }
}
