using Lucerne.Lending.CoreBanking;
using Lucerne.Lending.Integration;
using Lucerne.Lending.Workflow;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Lucerne.Lending.Api.Infrastructure;

public sealed class DomainExceptionFilter : IExceptionFilter
{
    private readonly ILogger<DomainExceptionFilter> _log;

    public DomainExceptionFilter(ILogger<DomainExceptionFilter> log) => _log = log;

    public void OnException(ExceptionContext context)
    {
        IActionResult? result = context.Exception switch
        {
            ValidationFailedException v => new BadRequestObjectResult(new ValidationProblemDetails(new Dictionary<string, string[]> { ["application"] = v.Errors.ToArray() })),
            ConflictException c => new ConflictObjectResult(new ProblemDetails { Title = c.Message, Status = 409 }),
            KeyNotFoundException => new NotFoundResult(),
            UnauthorizedAccessException u => new ObjectResult(new ProblemDetails { Title = u.Message, Status = 403 }) { StatusCode = 403 },
            CoreBankingException or BureauException or IntegrationException =>
                new ObjectResult(new ProblemDetails { Title = "A back-end system is not available. Please try again later.", Status = 503 }) { StatusCode = 503 },
            _ => null
        };
        if (result == null) return;
        if (result is ObjectResult { StatusCode: 503 }) _log.LogError(context.Exception, "Back-end failure");
        context.Result = result;
        context.ExceptionHandled = true;
    }
}
