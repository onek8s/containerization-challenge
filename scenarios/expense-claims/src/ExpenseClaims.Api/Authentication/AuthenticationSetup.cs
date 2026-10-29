using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ExpenseClaims.Api.Authentication;

public static class AuthenticationSetup
{
    public static IServiceCollection AddExpensesAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ExpensesAuthenticationOptions>(configuration.GetSection(ExpensesAuthenticationOptions.SectionName));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<ExpensesAuthenticationOptions>>((jwt, settings) =>
            {
                var options = settings.Value;
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = ExpenseClaimTypes.Upn,
                    RoleClaimType = ExpenseClaimTypes.Role,
                    ClockSkew = TimeSpan.FromMinutes(2)
                };

                if (options.IsDevelopmentMode)
                {
                    if (options.Development.SigningKey.Length < 32)
                        throw new InvalidOperationException("Authentication:Development:SigningKey must be at least 32 characters.");
                    jwt.TokenValidationParameters.ValidIssuer = options.Development.Issuer;
                    jwt.TokenValidationParameters.ValidAudience = options.Development.Audience;
                    jwt.TokenValidationParameters.IssuerSigningKey = DevelopmentSigningKey(options.Development);
                }
                else
                {
                    jwt.Authority = options.Adfs.Authority;
                    jwt.RequireHttpsMetadata = options.Adfs.RequireHttpsMetadata;
                    jwt.TokenValidationParameters.ValidAudiences = new[] { options.Adfs.Audience, "microsoft:identityserver:" + options.Adfs.Audience };
                    jwt.TokenValidationParameters.ValidIssuers = options.Adfs.ValidIssuers;
                }
            });

        services.AddAuthorization(auth =>
        {
            auth.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(ExpenseClaimTypes.Upn)
                .Build();
            auth.AddPolicy(Policies.Employee, p => p.RequireAuthenticatedUser().RequireClaim(ExpenseClaimTypes.Upn));
            auth.AddPolicy(Policies.Finance, p => p.RequireRole(Roles.Finance, Roles.Administrator));
            auth.AddPolicy(Policies.Administrator, p => p.RequireRole(Roles.Administrator));
        });

        return services;
    }

    public static SymmetricSecurityKey DevelopmentSigningKey(DevelopmentAuthOptions options) =>
        new(Encoding.UTF8.GetBytes(options.SigningKey));
}
