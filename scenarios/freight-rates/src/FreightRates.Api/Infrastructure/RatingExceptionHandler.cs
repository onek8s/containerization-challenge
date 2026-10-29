using FreightRates.Core;
using FreightRates.Data.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FreightRates.Api.Infrastructure;

public sealed class RatingExceptionHandler : IExceptionHandler
{
    private readonly ILogger<RatingExceptionHandler> _logger;

    public RatingExceptionHandler(ILogger<RatingExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails? problem = exception switch
        {
            QuoteValidationException v => new ValidationProblemDetails(new Dictionary<string, string[]> { ["shipment"] = v.Errors.ToArray() })
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The shipment is not valid.",
            },
            RatingException { Kind: RatingErrorKind.InvalidShipment } r => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The shipment cannot be rated.",
                Detail = r.Message,
            },
            RatingException { Kind: RatingErrorKind.PointNotServed } r => new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Origin or destination is not served.",
                Detail = r.Message,
            },
            RatingException r => new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Rating is temporarily unavailable.",
                Detail = "Please retry in a few minutes.",
            },
            _ => null,
        };

        if (problem is null)
            return false;

        if (problem.Status == StatusCodes.Status503ServiceUnavailable)
            _logger.LogError(exception, "Rating engine unavailable");

        httpContext.Response.StatusCode = problem.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, contentType: "application/problem+json", cancellationToken);
        return true;
    }
}
