using System;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Fabrikam.Housing.Hosting.Security
{
    public static class OpenIdConnectExtensions
    {
        /// <summary>
        /// Cookie + OpenID Connect sign-in against the Fabrikam identity server (Authentication section:
        /// Authority, ClientId, ClientSecret, RequireHttpsMetadata).
        /// </summary>
        public static IServiceCollection AddHousingSignIn(this IServiceCollection services, IConfiguration configuration, string cookieName, params string[] extraScopes)
        {
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
            var section = configuration.GetSection("Authentication");

            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                })
                .AddCookie(options =>
                {
                    options.Cookie.Name = cookieName;
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(section.GetValue("SessionMinutes", 60));
                    options.SlidingExpiration = true;
                    options.AccessDeniedPath = "/Home/AccessDenied";
                })
                .AddOpenIdConnect(options =>
                {
                    options.Authority = section["Authority"];
                    options.ClientId = section["ClientId"];
                    options.ClientSecret = section["ClientSecret"];
                    options.RequireHttpsMetadata = section.GetValue("RequireHttpsMetadata", true);
                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.UsePkce = true;
                    options.SaveTokens = true;
                    options.GetClaimsFromUserInfoEndpoint = true;
                    options.Scope.Clear();
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add(HousingScopes.HousingProfile);
                    foreach (var s in extraScopes) options.Scope.Add(s);
                    options.ClaimActions.MapUniqueJsonKey(HousingClaimTypes.TenancyReference, HousingClaimTypes.TenancyReference);
                    options.ClaimActions.MapUniqueJsonKey(HousingClaimTypes.TenantId, HousingClaimTypes.TenantId);
                    options.ClaimActions.MapUniqueJsonKey(HousingClaimTypes.Patch, HousingClaimTypes.Patch);
                    options.ClaimActions.MapJsonKey(HousingClaimTypes.Role, HousingClaimTypes.Role);
                    options.TokenValidationParameters.NameClaimType = "name";
                    options.TokenValidationParameters.RoleClaimType = HousingClaimTypes.Role;
                    if (!options.RequireHttpsMetadata)
                    {
                        // Plain http (development identity server): browsers drop SameSite=None cookies without Secure.
                        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                        options.NonceCookie.SameSite = SameSiteMode.Lax;
                    }
                });
            return services;
        }
    }
}
