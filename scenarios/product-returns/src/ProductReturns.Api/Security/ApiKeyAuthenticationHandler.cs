using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductReturns.Core;
using ProductReturns.Data;

namespace ProductReturns.Api.Security
{
    /// <summary>
    /// API keys for the warehouse scanners (stored hashed in rtn.ScannerDevice) and optionally for the portal.
    /// </summary>
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "ApiKey";

        private readonly ReturnsRepository _repository;
        private readonly IOptionsMonitor<ApiSecurityOptions> _security;

        public ApiKeyAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
            UrlEncoder encoder, ISystemClock clock, ReturnsRepository repository, IOptionsMonitor<ApiSecurityOptions> security)
            : base(options, logger, encoder, clock)
        {
            _repository = repository;
            _security = security;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(Headers.ApiKey, out var values))
            {
                return AuthenticateResult.NoResult();
            }

            var key = values.ToString();
            if (string.IsNullOrWhiteSpace(key))
            {
                return AuthenticateResult.Fail("Empty API key.");
            }

            var hash = ApiKeys.Hash(key.Trim());
            if (_security.CurrentValue.PortalKeyHashes.Any(h => string.Equals(h, hash, StringComparison.OrdinalIgnoreCase)))
            {
                return Success("portal", Roles.Portal);
            }

            var scanner = await _repository.FindScannerAsync(hash, Context.RequestAborted);
            if (scanner == null)
            {
                Logger.LogWarning("Unknown API key from {Address}", Context.Connection.RemoteIpAddress);
                return AuthenticateResult.Fail("Unknown API key.");
            }

            scanner.LastSeenUtc = DateTime.UtcNow;
            await _repository.SaveAsync(Context.RequestAborted);
            return Success(scanner.Name, Roles.Scanner);
        }

        private AuthenticateResult Success(string name, string role)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, name),
                new Claim(ClaimTypes.Role, role)
            }, SchemeName);
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
        }
    }
}
