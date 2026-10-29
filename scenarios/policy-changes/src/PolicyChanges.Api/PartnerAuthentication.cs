using Microsoft.EntityFrameworkCore;
using PolicyChanges.Data;
using PolicyChanges.Data.Entities;
using PolicyChanges.Data.Services;

namespace PolicyChanges.Api;

/// <summary>Resolves the partner from the <c>X-Api-Key</c> header.</summary>
public sealed class PartnerAuthentication : IEndpointFilter
{
    public const string Header = "X-Api-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var key = http.Request.Headers[Header].ToString();
        if (string.IsNullOrWhiteSpace(key))
            return Results.Problem(statusCode: 401, title: $"{Header} header is required.");

        var db = http.RequestServices.GetRequiredService<PolicyChangesContext>();
        var hash = ApiKeys.Hash(key);
        var partner = await db.Partners.AsNoTracking().FirstOrDefaultAsync(p => p.ApiKeyHash == hash && p.Active);
        if (partner is null)
            return Results.Problem(statusCode: 401, title: "Unknown or disabled API key.");

        http.Items[nameof(Partner)] = partner;
        return await next(context);
    }
}

public static class HttpContextPartnerExtensions
{
    public static Partner Partner(this HttpContext http) => (Partner)http.Items[nameof(Data.Entities.Partner)]!;
}
