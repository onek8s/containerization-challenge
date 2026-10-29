using System.Security.Claims;
using Lucerne.Lending.Contracts;
using Lucerne.Lending.CustomerPortal.Identity;
using Lucerne.Lending.Domain;
using Microsoft.AspNetCore.Identity;

namespace Lucerne.Lending.CustomerPortal.Infrastructure;

/// <summary>JSON endpoints used by the customer client under /portal-api. Every call is scoped to the signed-in person.</summary>
public static class PortalApi
{
    public const string RequestHeader = "X-Portal-Request";

    public static void MapPortalApi(this WebApplication app)
    {
        var api = app.MapGroupCompat("/portal-api");

        api.MapGet("/me", async (ClaimsPrincipal user, UserManager<PortalUser> users) =>
        {
            var u = await users.GetUserAsync(user);
            if (u == null) return Results.Unauthorized();
            return Results.Ok(new
            {
                name = $"{u.FirstName} {u.LastName}".Trim(),
                firstName = u.FirstName,
                lastName = u.LastName,
                email = u.Email,
                phone = u.PhoneNumber,
                personalIdentityCode = PersonalIdentityCode.Mask(u.PersonalIdentityCode),
                twoFactorEnabled = u.TwoFactorEnabled,
                lastSignInUtc = u.LastSignInUtc
            });
        });

        api.MapGet("/products", (LendingApiClient lending, CancellationToken ct) =>
            Forward(lending.GetAsync("api/products", null, ct)));

        api.MapPost("/calculate", (CalculatorRequest request, LendingApiClient lending, CancellationToken ct) =>
            Forward(lending.PostAsync("api/products/calculate", request, null, ct)));

        api.MapPost("/applications", async (ApplicationRequest request, ClaimsPrincipal user, UserManager<PortalUser> users, LendingApiClient lending, CancellationToken ct) =>
        {
            var u = await users.GetUserAsync(user);
            if (u == null) return Results.Unauthorized();
            // the main applicant is always the signed-in person, whatever the browser sends
            request.Channel = "Web";
            request.Applicant.PersonalIdentityCode = u.PersonalIdentityCode;
            if (string.IsNullOrWhiteSpace(request.Applicant.Email)) request.Applicant.Email = u.Email ?? "";
            if (request.CoApplicant != null && PersonalIdentityCode.IsValid(request.CoApplicant.PersonalIdentityCode)
                && string.Equals(request.CoApplicant.PersonalIdentityCode, u.PersonalIdentityCode, StringComparison.OrdinalIgnoreCase))
                return Results.Problem("The co-applicant must be another person.", statusCode: 400);
            return await Forward(lending.PostAsync("api/applications", request, Pic(user), ct));
        });

        api.MapGet("/applications", (ClaimsPrincipal user, LendingApiClient lending, CancellationToken ct) =>
            Forward(lending.GetAsync($"api/customers/{Uri.EscapeDataString(Pic(user))}/applications", Pic(user), ct)));

        api.MapGet("/applications/{reference}", (string reference, ClaimsPrincipal user, LendingApiClient lending, CancellationToken ct) =>
            Forward(lending.GetAsync($"api/applications/{Uri.EscapeDataString(reference)}?pic={Uri.EscapeDataString(Pic(user))}", Pic(user), ct)));

        api.MapPost("/applications/{reference}/accept", (string reference, AcceptOfferRequest request, ClaimsPrincipal user, LendingApiClient lending, CancellationToken ct) =>
            Forward(lending.PostAsync($"api/applications/{Uri.EscapeDataString(reference)}/accept?pic={Uri.EscapeDataString(Pic(user))}", request, Pic(user), ct)));

        api.MapPost("/applications/{reference}/cancel", (string reference, ClaimsPrincipal user, LendingApiClient lending, CancellationToken ct) =>
            Forward(lending.PostAsync($"api/applications/{Uri.EscapeDataString(reference)}/cancel?pic={Uri.EscapeDataString(Pic(user))}", Pic(user), ct)));

        api.MapGet("/applications/{reference}/documents/{id:long}", (string reference, long id, ClaimsPrincipal user, LendingApiClient lending, CancellationToken ct) =>
            Forward(lending.GetAsync($"api/applications/{Uri.EscapeDataString(reference)}/documents/{id}?pic={Uri.EscapeDataString(Pic(user))}", Pic(user), ct)));

        api.MapGet("/loans", (ClaimsPrincipal user, LendingApiClient lending, CancellationToken ct) =>
            Forward(lending.GetAsync($"api/customers/{Uri.EscapeDataString(Pic(user))}/loans", Pic(user), ct)));

        api.MapGet("/loans/{account}/payments", (string account, ClaimsPrincipal user, LendingApiClient lending, CancellationToken ct) =>
            Forward(lending.GetAsync($"api/customers/{Uri.EscapeDataString(Pic(user))}/loans/{Uri.EscapeDataString(account)}/payments", Pic(user), ct)));
    }

    private static string Pic(ClaimsPrincipal user) =>
        user.FindFirstValue(PortalClaims.PersonalIdentityCode) ?? throw new InvalidOperationException("Signed-in user has no identity code.");

    private static async Task<IResult> Forward(Task<HttpResponseMessage> call)
    {
        try
        {
            return new ForwardedResult(await call);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Results.Problem("The loan service is not available at the moment. Please try again later.", statusCode: 503);
        }
    }

    /// <summary>.NET 6 has no MapGroup; a small prefixing builder keeps the endpoint list readable.</summary>
    private static PrefixedRoutes MapGroupCompat(this WebApplication app, string prefix) => new(app, prefix);

    private sealed class PrefixedRoutes
    {
        private readonly WebApplication _app;
        private readonly string _prefix;

        public PrefixedRoutes(WebApplication app, string prefix)
        {
            _app = app;
            _prefix = prefix;
        }

        public void MapGet(string pattern, Delegate handler) => _app.MapGet(_prefix + pattern, handler).RequireAuthorization("Customer");
        public void MapPost(string pattern, Delegate handler) => _app.MapPost(_prefix + pattern, handler).RequireAuthorization("Customer");
    }
}

/// <summary>State-changing portal API calls must carry the custom header (simple CSRF guard together with SameSite cookies).</summary>
public sealed class PortalRequestHeaderMiddleware
{
    private readonly RequestDelegate _next;

    public PortalRequestHeaderMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/portal-api") && !HttpMethods.IsGet(context.Request.Method)
            && !context.Request.Headers.ContainsKey(PortalApi.RequestHeader))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return Task.CompletedTask;
        }
        return _next(context);
    }
}
