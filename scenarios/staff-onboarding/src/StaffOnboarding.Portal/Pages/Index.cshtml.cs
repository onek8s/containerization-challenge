using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using StaffOnboarding.Core.Requests;
using StaffOnboarding.Portal.Data;
using StaffOnboarding.Portal.Services;

namespace StaffOnboarding.Portal.Pages;

public class IndexModel(OnboardingRepository repository, CurrentUser user, ReferenceData reference, IOptions<PortalOptions> options) : PageModel
{
    public IReadOnlyList<TaskRow> Tasks { get; private set; } = Array.Empty<TaskRow>();
    public IReadOnlyList<RequestRow> MyRequests { get; private set; } = Array.Empty<RequestRow>();

    public async Task OnGetAsync()
    {
        Tasks = await repository.GetOpenTasksAsync(user.Account, user.GroupAssignees(reference.GroupAssignees(options.Value)));
        if (user.IsHr)
            MyRequests = await repository.ListRequestsAsync(null, null, user.Name, 25);
    }

    public static string Describe(TaskKind kind) => kind switch
    {
        TaskKind.ManagerApproval => "Manager approval",
        TaskKind.ITApproval => "IT security approval",
        TaskKind.HomeFolderHandover => "Home folder handover",
        _ => kind.ToString()
    };
}
