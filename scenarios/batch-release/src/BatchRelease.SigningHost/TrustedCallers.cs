using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace BatchRelease.SigningHost;

/// <summary>Only the configured application pool identities may call the signing host.</summary>
public sealed class TrustedCallerRequirement : IAuthorizationRequirement
{
    public const string Policy = "TrustedCaller";
}

public sealed class TrustedCallerHandler : AuthorizationHandler<TrustedCallerRequirement>
{
    private readonly IOptionsMonitor<SigningOptions> _options;
    private readonly ILogger<TrustedCallerHandler> _logger;

    public TrustedCallerHandler(IOptionsMonitor<SigningOptions> options, ILogger<TrustedCallerHandler> logger)
    {
        _options = options;
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TrustedCallerRequirement requirement)
    {
        var name = context.User.Identity?.IsAuthenticated == true ? context.User.Identity.Name : null;
        if (name is not null && _options.CurrentValue.TrustedCallers.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning("Rejected call from {Caller}", name ?? "(anonymous)");
        }
        return Task.CompletedTask;
    }
}
