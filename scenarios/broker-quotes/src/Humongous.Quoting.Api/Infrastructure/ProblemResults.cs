using FluentValidation.Results;

namespace Humongous.Quoting.Api.Infrastructure;

public static class ProblemResults
{
    public static IResult Validation(ValidationResult result) =>
        Results.ValidationProblem(result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));

    public static IResult Validation(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    public static IResult Conflict(string message) =>
        Results.Problem(message, statusCode: StatusCodes.Status409Conflict, title: "Conflict");
}
