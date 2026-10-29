using System;
using System.Net;
using Lucerne.ProjectSites.SharePoint.Auth;
using Microsoft.SharePoint.Client;

namespace Lucerne.ProjectSites.SharePoint.Csom
{
    public interface IClientContextFactory
    {
        ClientContext Create(string siteUrl);
    }

    /// <summary>App-only context with a high-trust S2S token (sync service, background work in the add-in).</summary>
    public class HighTrustAppOnlyContextFactory : IClientContextFactory
    {
        private readonly HighTrustTokenIssuer _issuer;

        public HighTrustAppOnlyContextFactory(HighTrustTokenIssuer issuer)
        {
            _issuer = issuer;
        }

        public ClientContext Create(string siteUrl)
        {
            var uri = new Uri(siteUrl);
            var token = _issuer.CreateAppOnlyAccessToken(uri);
            return BearerContext(siteUrl, token);
        }

        internal static ClientContext BearerContext(string siteUrl, string token)
        {
            var ctx = new ClientContext(siteUrl)
            {
                AuthenticationMode = ClientAuthenticationMode.Anonymous,
                FormDigestHandlingEnabled = false,
                RequestTimeout = 180000
            };
            ctx.ExecutingWebRequest += (s, e) => e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + token;
            return ctx;
        }
    }

    /// <summary>User + app context for requests of the add-in, the user is identified by the Windows SID.</summary>
    public class HighTrustUserContextFactory : IClientContextFactory
    {
        private readonly HighTrustTokenIssuer _issuer;
        private readonly string _userSid;

        public HighTrustUserContextFactory(HighTrustTokenIssuer issuer, string userSid)
        {
            _issuer = issuer;
            _userSid = userSid;
        }

        public ClientContext Create(string siteUrl)
        {
            return HighTrustAppOnlyContextFactory.BearerContext(siteUrl, _issuer.CreateUserAccessToken(new Uri(siteUrl), _userSid));
        }
    }

    /// <summary>Windows credentials of the current process (admin tool run by a farm administrator).</summary>
    public class WindowsContextFactory : IClientContextFactory
    {
        private readonly ICredentials _credentials;

        public WindowsContextFactory(ICredentials credentials = null)
        {
            _credentials = credentials ?? CredentialCache.DefaultNetworkCredentials;
        }

        public ClientContext Create(string siteUrl)
        {
            return new ClientContext(siteUrl) { Credentials = _credentials, RequestTimeout = 180000 };
        }
    }
}
