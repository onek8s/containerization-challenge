using System;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ProductReturns.Api.Security
{
    public class PortalCallerRequirement : IAuthorizationRequirement
    {
    }

    /// <summary>
    /// The portal calls the API with its service account (Kerberos) or, where configured, with a portal API key.
    /// </summary>
    public class PortalCallerHandler : AuthorizationHandler<PortalCallerRequirement>
    {
        private readonly IOptionsMonitor<ApiSecurityOptions> _options;
        private readonly ILogger<PortalCallerHandler> _logger;

        public PortalCallerHandler(IOptionsMonitor<ApiSecurityOptions> options, ILogger<PortalCallerHandler> logger)
        {
            _options = options;
            _logger = logger;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PortalCallerRequirement requirement)
        {
            var user = context.User;
            if (user.IsInRole(Roles.Portal))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            var name = user.Identity?.Name;
            if (user.Identity is WindowsIdentity || user.Identity?.AuthenticationType == "Kerberos" || user.Identity?.AuthenticationType == "NTLM"
                || user.Identity?.AuthenticationType == "Negotiate")
            {
                if (name != null && _options.CurrentValue.PortalAccounts.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase)))
                {
                    context.Succeed(requirement);
                    return Task.CompletedTask;
                }
            }

            _logger.LogWarning("Caller {Name} is not a portal account", name);
            return Task.CompletedTask;
        }
    }
}
