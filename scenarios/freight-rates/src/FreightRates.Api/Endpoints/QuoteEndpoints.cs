using FreightRates.Api.Infrastructure;
using FreightRates.Core;
using FreightRates.Data;
using FreightRates.Data.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace FreightRates.Api.Endpoints;

public static class QuoteEndpoints
{
    public static void MapQuoteEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization().WithOpenApi();

        api.MapPost("/quotes", CreateQuote)
            .WithName("CreateQuote")
            .WithSummary("Rates an LTL shipment and stores the quote.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        api.MapGet("/quotes/{number}", GetQuote).WithName("GetQuote");

        api.MapGet("/quotes", ListQuotes).WithName("ListQuotes");

        api.MapGet("/accessorials", async (FreightRatesContext db, CancellationToken ct) =>
            await db.Accessorials.AsNoTracking()
                .Where(a => a.Active)
                .OrderBy(a => a.Code)
                .Select(a => new { a.Code, a.Description, a.Amount, a.PerCwt })
                .ToListAsync(ct))
            .WithName("ListAccessorials");

        api.MapGet("/tariff", async (IRatingEngine engine, CancellationToken ct) =>
            {
                var info = await engine.GetTariffInfoAsync(ct);
                return new { tariff = info.TariffName, effectiveDate = info.EffectiveDate };
            })
            .WithName("GetTariff");

        api.MapGet("/freight-class", (int weightLbs, decimal lengthIn, decimal widthIn, decimal heightIn, int? pieces) =>
            {
                var pcf = FreightClass.DensityPcf(weightLbs, lengthIn * (pieces ?? 1), widthIn, heightIn);
                return Results.Ok(new { densityPcf = pcf, freightClass = FreightClass.FromDensity(pcf) });
            })
            .WithName("EstimateFreightClass")
            .WithSummary("Estimates the density based freight class.");
    }

    private static async Task<Created<QuoteResponse>> CreateQuote(
        QuoteRequest request,
        HttpContext http,
        FreightRatesContext db,
        QuoteService quotes,
        CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == http.User.CustomerId(), ct);
        var quote = await quotes.CreateQuoteAsync(customer, request.ToShipment(), request.CustomerReference, ct);
        return TypedResults.Created($"/api/v1/quotes/{quote.Number}", QuoteResponse.From(quote));
    }

    private static async Task<Results<Ok<QuoteResponse>, NotFound>> GetQuote(string number, HttpContext http, QuoteService quotes, CancellationToken ct)
    {
        var quote = await quotes.FindAsync(http.User.CustomerId(), number, ct);
        return quote is null ? TypedResults.NotFound() : TypedResults.Ok(QuoteResponse.From(quote));
    }

    private static async Task<Ok<List<QuoteResponse>>> ListQuotes(HttpContext http, QuoteService quotes, int? take, CancellationToken ct)
    {
        var list = await quotes.RecentAsync(http.User.CustomerId(), Math.Clamp(take ?? 20, 1, 200), ct);
        return TypedResults.Ok(list.Select(QuoteResponse.From).ToList());
    }
}
