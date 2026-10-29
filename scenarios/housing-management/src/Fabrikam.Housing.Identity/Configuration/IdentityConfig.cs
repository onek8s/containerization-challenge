using System.Collections.Generic;
using Fabrikam.Housing.Hosting.Security;
using IdentityServer4;
using IdentityServer4.Models;
using Microsoft.Extensions.Configuration;

namespace Fabrikam.Housing.Identity.Configuration
{
    /// <summary>
    /// Resources and the interactive clients. Base URLs and secrets come from the Clients section.
    /// Contractor API clients are loaded from the housing database (ContractorClientStore).
    /// </summary>
    public static class IdentityConfig
    {
        public static IEnumerable<IdentityResource> IdentityResources => new IdentityResource[]
        {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
            new IdentityResources.Email(),
            new IdentityResource(HousingScopes.HousingProfile, "Fabrikam Housing profile", new[]
            {
                HousingClaimTypes.Role, HousingClaimTypes.TenancyReference, HousingClaimTypes.TenantId, HousingClaimTypes.Patch
            })
        };

        public static IEnumerable<ApiScope> ApiScopes => new[]
        {
            new ApiScope(HousingScopes.ContractorApi, "Fabrikam contractor API", new[] { HousingClaimTypes.ContractorCode })
        };

        public static IEnumerable<ApiResource> ApiResources => new[]
        {
            new ApiResource(HousingScopes.ContractorApi, "Fabrikam contractor API", new[] { HousingClaimTypes.ContractorCode, HousingClaimTypes.Role })
            {
                Scopes = { HousingScopes.ContractorApi }
            }
        };

        public static IEnumerable<Client> Clients(IConfiguration configuration)
        {
            var section = configuration.GetSection("Clients");
            string Url(string name) => section[name + ":BaseUrl"]?.TrimEnd('/');

            var tenants = Url("TenantPortal");
            var housing = Url("BackOffice");
            var jobs = Url("Jobs");
            var api = Url("ContractorApi");

            yield return new Client
            {
                ClientId = "tenant-portal",
                ClientName = "Fabrikam Homes tenant portal",
                ClientSecrets = { new Secret(section["TenantPortal:Secret"].Sha256()) },
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RedirectUris = { tenants + "/signin-oidc" },
                PostLogoutRedirectUris = { tenants + "/signout-callback-oidc", tenants + "/" },
                FrontChannelLogoutUri = tenants + "/signout-oidc",
                AllowedScopes =
                {
                    IdentityServerConstants.StandardScopes.OpenId,
                    IdentityServerConstants.StandardScopes.Profile,
                    IdentityServerConstants.StandardScopes.Email,
                    HousingScopes.HousingProfile
                },
                AllowOfflineAccess = false,
                RequireConsent = false
            };

            yield return new Client
            {
                ClientId = "back-office",
                ClientName = "Fabrikam Housing",
                ClientSecrets = { new Secret(section["BackOffice:Secret"].Sha256()) },
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RedirectUris = { housing + "/signin-oidc" },
                PostLogoutRedirectUris = { housing + "/signout-callback-oidc", housing + "/" },
                FrontChannelLogoutUri = housing + "/signout-oidc",
                AllowedScopes =
                {
                    IdentityServerConstants.StandardScopes.OpenId,
                    IdentityServerConstants.StandardScopes.Profile,
                    IdentityServerConstants.StandardScopes.Email,
                    HousingScopes.HousingProfile
                },
                AlwaysIncludeUserClaimsInIdToken = true,
                RequireConsent = false
            };

            yield return new Client
            {
                ClientId = "housing-jobs",
                ClientName = "Fabrikam Housing jobs dashboard",
                ClientSecrets = { new Secret(section["Jobs:Secret"].Sha256()) },
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RedirectUris = { jobs + "/signin-oidc" },
                PostLogoutRedirectUris = { jobs + "/signout-callback-oidc", jobs + "/" },
                AllowedScopes =
                {
                    IdentityServerConstants.StandardScopes.OpenId,
                    IdentityServerConstants.StandardScopes.Profile,
                    HousingScopes.HousingProfile
                },
                AlwaysIncludeUserClaimsInIdToken = true,
                RequireConsent = false
            };

            yield return new Client
            {
                ClientId = "contractor-api-swagger",
                ClientName = "Contractor API documentation",
                RequireClientSecret = false,
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RedirectUris = { api + "/swagger/oauth2-redirect.html" },
                AllowedCorsOrigins = { api },
                AllowedScopes = { HousingScopes.ContractorApi },
                RequireConsent = false
            };
        }
    }
}
