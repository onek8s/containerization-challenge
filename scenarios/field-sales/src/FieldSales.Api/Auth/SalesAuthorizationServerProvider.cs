using System;
using System.Linq;
using System.Threading.Tasks;
using log4net;
using Microsoft.AspNet.Identity;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.OAuth;
using Wingtip.FieldSales.Api.Infrastructure;
using Wingtip.FieldSales.Data;

namespace Wingtip.FieldSales.Api.Auth
{
    /// <summary>
    /// Token endpoint for the desktop client: resource owner password grant for the sign-in dialog and refresh
    /// token grant so that reps stay signed in for the configured number of days while working offline.
    /// </summary>
    public class SalesAuthorizationServerProvider : OAuthAuthorizationServerProvider
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SalesAuthorizationServerProvider));
        private readonly Func<ApplicationUserManager> _userManagerFactory;
        private readonly ApiSettings _settings;

        public SalesAuthorizationServerProvider(Func<ApplicationUserManager> userManagerFactory, ApiSettings settings)
        {
            _userManagerFactory = userManagerFactory;
            _settings = settings;
        }

        public override Task ValidateClientAuthentication(OAuthValidateClientAuthenticationContext context)
        {
            string clientId, clientSecret;
            if (!context.TryGetBasicCredentials(out clientId, out clientSecret)) context.TryGetFormCredentials(out clientId, out clientSecret);

            // The desktop client is a public client: it cannot keep a secret, so only the id is checked.
            if (clientId == _settings.DesktopClientId) context.Validated(clientId);
            else context.SetError("invalid_client", "Unknown client.");
            return Task.FromResult(0);
        }

        public override async Task GrantResourceOwnerCredentials(OAuthGrantResourceOwnerCredentialsContext context)
        {
            using (var users = _userManagerFactory())
            {
                var user = await users.FindByNameAsync(context.UserName);
                if (user == null)
                {
                    Log.WarnFormat("Sign-in failed for unknown user {0} from {1}", context.UserName, context.Request.RemoteIpAddress);
                    context.SetError("invalid_grant", "The user name or password is incorrect.");
                    return;
                }

                if (await users.IsLockedOutAsync(user.Id))
                {
                    Log.WarnFormat("Sign-in refused for locked out user {0}", user.UserName);
                    context.SetError("invalid_grant", "The account is locked. Try again later or contact the sales office.");
                    return;
                }

                if (!await users.CheckPasswordAsync(user, context.Password))
                {
                    await users.AccessFailedAsync(user.Id);
                    Log.WarnFormat("Sign-in failed for {0} from {1}", user.UserName, context.Request.RemoteIpAddress);
                    context.SetError("invalid_grant", "The user name or password is incorrect.");
                    return;
                }

                var roles = await users.GetRolesAsync(user.Id);
                if (!roles.Contains(Roles.SalesRep) && !roles.Contains(Roles.SalesManager))
                {
                    context.SetError("invalid_grant", "The account is not enabled for Field Sales.");
                    return;
                }

                await users.ResetAccessFailedCountAsync(user.Id);
                var identity = await user.CreateIdentityAsync(users, OAuthDefaults.AuthenticationType);
                var properties = new AuthenticationProperties(new System.Collections.Generic.Dictionary<string, string>
                {
                    { "as:client_id", context.ClientId ?? "" },
                    { "userName", user.UserName }
                });

                Log.InfoFormat("Token issued to {0} ({1})", user.UserName, string.Join(",", roles));
                context.Validated(new AuthenticationTicket(identity, properties));
            }
        }

        public override async Task GrantRefreshToken(OAuthGrantRefreshTokenContext context)
        {
            string originalClient;
            context.Ticket.Properties.Dictionary.TryGetValue("as:client_id", out originalClient);
            if (originalClient != context.ClientId)
            {
                context.SetError("invalid_grant", "The refresh token was issued to another client.");
                return;
            }

            // Re-read the user so that a disabled or changed account (discount limit, roles) takes effect at refresh.
            using (var users = _userManagerFactory())
            {
                var user = await users.FindByNameAsync(context.Ticket.Identity.Name);
                if (user == null || await users.IsLockedOutAsync(user.Id))
                {
                    context.SetError("invalid_grant", "The account is not available.");
                    return;
                }

                var identity = await user.CreateIdentityAsync(users, OAuthDefaults.AuthenticationType);
                context.Validated(new AuthenticationTicket(identity, context.Ticket.Properties));
            }
        }

        public override Task TokenEndpoint(OAuthTokenEndpointContext context)
        {
            foreach (var property in context.Properties.Dictionary.Where(p => !p.Key.StartsWith(".") && !p.Key.StartsWith("as:")))
                context.AdditionalResponseParameters.Add(property.Key, property.Value);
            return Task.FromResult(0);
        }
    }
}
