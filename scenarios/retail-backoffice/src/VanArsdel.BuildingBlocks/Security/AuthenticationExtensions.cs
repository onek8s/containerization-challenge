using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace VanArsdel.BuildingBlocks.Security
{
    public class AuthenticationSettings
    {
        /// <summary>Accepted audiences (App ID URI of the API registration and the ADFS relying party identifier).</summary>
        public List<string> Audiences { get; set; } = new List<string>();

        /// <summary>Token issuers in order of preference. Entra ID first, ADFS as the on-premises fallback.</summary>
        public List<AuthoritySettings> Authorities { get; set; } = new List<AuthoritySettings>();

        public bool RequireHttpsMetadata { get; set; } = true;
    }

    public class AuthoritySettings
    {
        public string Name { get; set; }
        public string Authority { get; set; }

        /// <summary>Issuer value in the tokens. ADFS issues "http://.../adfs/services/trust" which differs from the authority.</summary>
        public string Issuer { get; set; }

        public string RoleClaimType { get; set; } = "roles";
    }

    public static class AuthenticationExtensions
    {
        public const string PolicySchemeName = "MultiIssuer";

        public static AuthenticationBuilder AddVanArsdelJwt(this IServiceCollection services, IConfiguration configuration)
        {
            var settings = configuration.GetSection("Authentication").Get<AuthenticationSettings>() ?? new AuthenticationSettings();
            if (settings.Authorities.Count == 0)
            {
                throw new InvalidOperationException("Authentication:Authorities is not configured.");
            }

            JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

            var builder = services.AddAuthentication(options =>
            {
                options.DefaultScheme = PolicySchemeName;
                options.DefaultChallengeScheme = PolicySchemeName;
            });

            foreach (var authority in settings.Authorities)
            {
                var a = authority;
                builder.AddJwtBearer(SchemeName(a), options =>
                {
                    options.Authority = a.Authority;
                    options.RequireHttpsMetadata = settings.RequireHttpsMetadata;
#if !NETCOREAPP3_1
                    options.MapInboundClaims = false;
#endif
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidAudiences = settings.Audiences,
                        ValidIssuer = string.IsNullOrEmpty(a.Issuer) ? null : a.Issuer,
                        ValidateIssuer = !string.IsNullOrEmpty(a.Issuer),
                        NameClaimType = "name",
                        RoleClaimType = a.RoleClaimType,
                        ClockSkew = TimeSpan.FromMinutes(2),
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = ctx =>
                        {
                            // SignalR sends the token on the query string for WebSockets.
                            var token = ctx.Request.Query["access_token"];
                            if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                            {
                                ctx.Token = token;
                            }
                            return System.Threading.Tasks.Task.CompletedTask;
                        },
                    };
                });
            }

            builder.AddPolicyScheme(PolicySchemeName, "Entra ID or ADFS", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    var issuer = ReadIssuer(context.Request.Headers["Authorization"].ToString(), context.Request.Query["access_token"]);
                    var match = settings.Authorities.FirstOrDefault(a =>
                        issuer != null && (string.Equals(a.Issuer, issuer, StringComparison.OrdinalIgnoreCase)
                                           || issuer.StartsWith(a.Authority.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)));
                    return SchemeName(match ?? settings.Authorities[0]);
                };
            });

            return builder;
        }

        public static void AddScopePolicy(this AuthorizationOptions options, string policyName, string scope, params string[] roles)
        {
            options.AddPolicy(policyName, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx => HasScope(ctx.User, scope)
                                               || ctx.User.HasAnyRole(roles)
                                               || ctx.User.HasAnyRole(Contracts.Roles.Service, Contracts.Roles.Admin));
            });
        }

        public static bool HasScope(ClaimsPrincipal user, string scope)
        {
            foreach (var claim in user.Claims.Where(c => c.Type == "scp" || c.Type == "scope"))
            {
                if (claim.Value.Split(' ').Any(s => s == scope || s.EndsWith("/" + scope, StringComparison.Ordinal)))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool HasAnyRole(this ClaimsPrincipal user, params string[] roles)
        {
            if (roles == null || roles.Length == 0) return false;
            var userRoles = user.Claims
                .Where(c => c.Type == "roles" || c.Type == "role" || c.Type == ClaimTypes.Role)
                .Select(c => c.Value);
            return userRoles.Any(r => roles.Contains(r, StringComparer.OrdinalIgnoreCase));
        }

        public static string StoreCode(this ClaimsPrincipal user) =>
            user.FindFirst("store_code")?.Value ?? user.FindFirst("extension_StoreCode")?.Value;

        public static string UserName(this ClaimsPrincipal user) =>
            user.FindFirst("preferred_username")?.Value ?? user.FindFirst("upn")?.Value
            ?? user.FindFirst("unique_name")?.Value ?? user.FindFirst("client_id")?.Value
            ?? user.FindFirst("azp")?.Value ?? "unknown";

        private static string SchemeName(AuthoritySettings a) => "Bearer-" + (a.Name ?? "Default");

        private static string ReadIssuer(string authorizationHeader, string queryToken)
        {
            var raw = authorizationHeader != null && authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authorizationHeader.Substring(7).Trim()
                : queryToken;
            if (string.IsNullOrEmpty(raw)) return null;
            try
            {
                return new JwtSecurityTokenHandler().ReadJwtToken(raw).Issuer;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
