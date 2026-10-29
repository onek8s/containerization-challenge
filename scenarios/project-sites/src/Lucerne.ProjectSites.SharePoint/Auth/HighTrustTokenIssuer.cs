using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;

namespace Lucerne.ProjectSites.SharePoint.Auth
{
    /// <summary>
    /// Issues server-to-server (high-trust) access tokens for SharePoint on-premises. The farm trusts the
    /// signing certificate through an SPTrustedSecurityTokenIssuer registered with the issuer id; the add-in
    /// and the sync service are registered as app principals with their client id.
    /// Replaces the TokenHelper.cs from the Visual Studio template (which needed WIF 3.5 on the server).
    /// </summary>
    public class HighTrustTokenIssuer
    {
        /// <summary>Well-known principal id of SharePoint.</summary>
        public const string SharePointPrincipal = "00000003-0000-0ff1-ce00-000000000000";

        public const string TrustedForDelegationClaim = "trustedfordelegation";
        public const string NameIdClaim = "nameid";
        public const string IdentityProviderClaim = "nii";
        public const string ActorTokenClaim = "actortoken";
        public const string ActiveDirectoryProvider = "urn:office:idp:activedirectory";

        private static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);
        private readonly X509Certificate2 _certificate;
        private readonly Func<DateTime> _clock;

        public HighTrustTokenIssuer(X509Certificate2 certificate, string issuerId, string clientId, string realm)
            : this(certificate, issuerId, clientId, realm, () => DateTime.UtcNow)
        {
        }

        public HighTrustTokenIssuer(X509Certificate2 certificate, string issuerId, string clientId, string realm, Func<DateTime> clock)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            if (!certificate.HasPrivateKey)
            {
                throw new ArgumentException("The signing certificate has no private key.", nameof(certificate));
            }

            _certificate = certificate;
            IssuerId = issuerId;
            ClientId = clientId;
            Realm = realm;
            _clock = clock;
        }

        public string IssuerId { get; }
        public string ClientId { get; }
        public string Realm { get; }

        /// <summary>App-only token: only the signed actor token, used for background work.</summary>
        public string CreateAppOnlyAccessToken(Uri targetSite)
        {
            return CreateActorToken(targetSite, false);
        }

        /// <summary>
        /// User + app token. The user is identified by the Windows SID (the add-in site uses Windows
        /// authentication to find out who the user is).
        /// </summary>
        public string CreateUserAccessToken(Uri targetSite, string userSid)
        {
            if (string.IsNullOrEmpty(userSid))
            {
                throw new ArgumentNullException(nameof(userSid));
            }

            var actorToken = CreateActorToken(targetSite, true);
            var now = _clock();

            var payload = new JwtPayload
            {
                { "aud", Audience(targetSite) },
                { "iss", ClientId + "@" + Realm },
                { "nbf", EpochTime.GetIntDate(now) },
                { "exp", EpochTime.GetIntDate(now.Add(Lifetime)) },
                { NameIdClaim, userSid.ToLowerInvariant() },
                { IdentityProviderClaim, ActiveDirectoryProvider },
                { ActorTokenClaim, actorToken }
            };

            // The outer token is not signed; SharePoint trusts it through the signed actor token.
            var header = new JwtHeader { { "alg", "none" } };
            var outer = new JwtSecurityToken(header, payload);
            return new JwtSecurityTokenHandler().WriteToken(outer);
        }

        public string Audience(Uri targetSite)
        {
            return SharePointPrincipal + "/" + targetSite.Authority + "@" + Realm;
        }

        private string CreateActorToken(Uri targetSite, bool trustedForDelegation)
        {
            var now = _clock();
            var claims = new List<Claim>
            {
                new Claim(NameIdClaim, ClientId + "@" + Realm)
            };

            if (trustedForDelegation)
            {
                claims.Add(new Claim(TrustedForDelegationClaim, "true"));
            }

            var credentials = new X509SigningCredentials(_certificate, SecurityAlgorithms.RsaSha256);
            var token = new JwtSecurityToken(
                issuer: IssuerId + "@" + Realm,
                audience: Audience(targetSite),
                claims: claims,
                notBefore: now,
                expires: now.Add(Lifetime),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
