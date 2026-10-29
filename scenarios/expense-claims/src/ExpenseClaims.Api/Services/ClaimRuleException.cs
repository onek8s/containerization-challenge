namespace ExpenseClaims.Api.Services;

/// <summary>A request that breaks a claim workflow rule. Returned to the client as problem details.</summary>
public sealed class ClaimRuleException : Exception
{
    public ClaimRuleException(int statusCode, string message) : base(message) => StatusCode = statusCode;

    public int StatusCode { get; }

    public static ClaimRuleException NotFound(string what = "Claim") => new(StatusCodes.Status404NotFound, $"{what} was not found.");
    public static ClaimRuleException Forbidden(string message) => new(StatusCodes.Status403Forbidden, message);
    public static ClaimRuleException Conflict(string message) => new(StatusCodes.Status409Conflict, message);
    public static ClaimRuleException Invalid(string message) => new(StatusCodes.Status422UnprocessableEntity, message);
}
