using Microsoft.AspNetCore.Authorization;

namespace Wingtip.Invoices.Admin.Services;

public class CurrentUser
{
    private readonly IHttpContextAccessor accessor;
    private readonly IAuthorizationService authorization;
    private readonly UserDirectory directory;

    public CurrentUser(IHttpContextAccessor accessor, IAuthorizationService authorization, UserDirectory directory)
    {
        this.accessor = accessor;
        this.authorization = authorization;
        this.directory = directory;
    }

    public string Account => accessor.HttpContext?.User.Identity?.Name ?? "anonymous";

    public string DisplayName => directory.DisplayName(Account);

    public async Task<bool> IsAdminAsync()
    {
        var user = accessor.HttpContext?.User;
        return user != null && (await authorization.AuthorizeAsync(user, Policies.Admin)).Succeeded;
    }
}
