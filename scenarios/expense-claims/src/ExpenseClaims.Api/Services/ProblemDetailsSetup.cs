using Microsoft.AspNetCore.Mvc;

namespace ExpenseClaims.Api.Services;

public static class ProblemDetailsSetup
{
    public static IServiceCollection AddProblemDetailsForClaimRules(this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(o =>
        {
            o.ClientErrorMapping[StatusCodes.Status422UnprocessableEntity] = new ClientErrorData { Title = "The request breaks an expense rule." };
        });
        return services;
    }

    /// <summary>Turns <see cref="ClaimRuleException"/> into an RFC 7807 problem response.</summary>
    public static IApplicationBuilder UseClaimRuleProblemDetails(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (ClaimRuleException ex) when (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode = ex.StatusCode;
                await context.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = ex.StatusCode,
                    Title = ex.Message,
                    Instance = context.Request.Path
                }, options: null, contentType: "application/problem+json");
            }
        });
}
