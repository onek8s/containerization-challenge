using ContosoFoods.TradeRebates.Api;
using ContosoFoods.TradeRebates.Core;
using ContosoFoods.TradeRebates.Data;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<RebateDatabase>();
builder.Services.AddScoped<IDistributorApiStore, SqlRebateStore>();
builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/ping", () => Results.Text("ok")).AllowAnonymous();

var v1 = app.MapGroup("/api/v1").RequireAuthorization();

v1.MapPost("/claims", async (SubmitClaimRequest request, HttpContext http, IDistributorApiStore store) =>
{
    var errors = ClaimRequestValidator.Validate(request);
    if (errors.Count > 0)
        return Results.ValidationProblem(errors);

    try
    {
        int claimId = await store.SubmitClaimAsync(http.User.DistributorId(), request.Period, request.Reference,
            http.User.Identity!.Name!, ClaimRequestValidator.ToInput(request));
        ClaimDetail claim = (await store.GetClaimAsync(claimId))!;
        var h = claim.Header;
        return Results.Created($"/api/v1/claims/{claimId}",
            new SubmitClaimResponse(claimId, ClaimStatus.Name(h.Status), h.LineCount, h.WarningCount, h.RejectedCount, h.TotalAmount));
    }
    catch (RebateRuleException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
    }
});

v1.MapGet("/claims", async (int? period, HttpContext http, IDistributorApiStore store) =>
    Results.Ok(await store.ListClaimsAsync(http.User.DistributorId(), period)));

v1.MapGet("/claims/{id:int}", async (int id, HttpContext http, IDistributorApiStore store) =>
{
    ClaimDetail? claim = await store.GetClaimAsync(id);
    if (claim is null || claim.Header.DistributorId != http.User.DistributorId())
        return Results.NotFound();
    var h = claim.Header;
    return Results.Ok(new
    {
        h.ClaimId,
        h.Period,
        Status = ClaimStatus.Name(h.Status),
        h.LineCount,
        h.TotalQuantity,
        h.TotalAmount,
        h.WarningCount,
        h.RejectedCount,
        h.ReceivedAt,
        h.DecidedAt,
        Lines = claim.Lines.Select(l => new { l.LineNumber, l.InvoiceNo, l.InvoiceDate, l.Gtin, l.Quantity, l.NetAmount, Status = LineStatus.Name(l.LineStatus), l.Issue }),
        Issues = claim.Issues
    });
});

v1.MapGet("/agreements", async (HttpContext http, IDistributorApiStore store) =>
    Results.Ok(await store.ListAgreementsAsync(http.User.DistributorId())));

app.Run();

public partial class Program;
