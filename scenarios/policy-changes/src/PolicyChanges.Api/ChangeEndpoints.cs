using System.Text.Json.Serialization;
using PolicyChanges.Core.Requests;
using PolicyChanges.Data.Entities;
using PolicyChanges.Data.Services;

namespace PolicyChanges.Api;

public sealed class ChangeSubmission
{
    public string PolicyNumber { get; set; } = "";
    public ChangeType ChangeType { get; set; }
    public DateOnly EffectiveDate { get; set; }

    /// <summary>The partner's own id for the change; a partner reference is accepted once.</summary>
    public string? PartnerReference { get; set; }

    public Dictionary<string, string> Fields { get; set; } = new();
}

public sealed record ChangeStatusResponse(
    string Reference,
    string? PartnerReference,
    string PolicyNumber,
    ChangeType ChangeType,
    DateOnly EffectiveDate,
    ChangeStatus Status,
    string? Endorsement,
    string? Message,
    DateTime CreatedUtc,
    DateTime? CompletedUtc)
{
    public static ChangeStatusResponse From(ChangeRequest r) => new(
        r.Reference, r.PartnerReference, r.PolicyNumber, r.ChangeType, r.EffectiveDate, r.Status,
        r.Endorsement,
        // Messages of failed attempts are internal; partners see the final outcome only.
        r.Status is ChangeStatus.Applied or ChangeStatus.Rejected ? r.ResultMessage : null,
        r.CreatedUtc, r.CompletedUtc);
}

public static class ChangeEndpoints
{
    public static void MapChangeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/changes")
            .AddEndpointFilter<PartnerAuthentication>()
            .WithTags("Changes");

        group.MapPost("/", async (ChangeSubmission body, HttpContext http, ChangeService changes, CancellationToken ct) =>
        {
            var partner = http.Partner();
            var draft = new ChangeDraft
            {
                PolicyNumber = body.PolicyNumber,
                ChangeType = body.ChangeType,
                EffectiveDate = body.EffectiveDate,
                Fields = new Dictionary<string, string>(body.Fields ?? new(), StringComparer.OrdinalIgnoreCase),
            };
            var result = await changes.SubmitAsync(draft, $"partner:{partner.Code}", ChangeChannel.Partner, partner, body.PartnerReference, ct);
            if (!result.Succeeded)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["change"] = result.Errors.ToArray() });

            var location = $"/api/v1/changes/{result.Request!.Reference}";
            return Results.Accepted(location, ChangeStatusResponse.From(result.Request));
        })
        .Produces<ChangeStatusResponse>(StatusCodes.Status202Accepted)
        .ProducesValidationProblem();

        group.MapGet("/{reference}", async (string reference, HttpContext http, ChangeService changes, CancellationToken ct) =>
        {
            var r = await changes.FindAsync(reference.ToUpperInvariant(), ct);
            return r is null || r.PartnerId != http.Partner().Id ? Results.NotFound() : Results.Ok(ChangeStatusResponse.From(r));
        })
        .Produces<ChangeStatusResponse>();

        group.MapGet("/", async (string? partnerReference, string? policyNumber, HttpContext http, ChangeService changes, CancellationToken ct) =>
        {
            var partner = http.Partner();
            var list = await changes.ListAsync(null, policyNumber, 500, ct);
            return Results.Ok(list
                .Where(r => r.PartnerId == partner.Id)
                .Where(r => partnerReference is null || r.PartnerReference == partnerReference)
                .Take(100)
                .Select(ChangeStatusResponse.From));
        })
        .Produces<IEnumerable<ChangeStatusResponse>>();

        group.MapDelete("/{reference}", async (string reference, HttpContext http, ChangeService changes, CancellationToken ct) =>
        {
            var partner = http.Partner();
            var r = await changes.FindAsync(reference.ToUpperInvariant(), ct);
            if (r is null || r.PartnerId != partner.Id) return Results.NotFound();
            return await changes.WithdrawAsync(r.Reference, $"partner:{partner.Code}", "withdrawn by partner", ct)
                ? Results.NoContent()
                : Results.Problem(statusCode: 409, title: $"{r.Reference} is {r.Status} and can no longer be withdrawn.");
        });
    }
}
