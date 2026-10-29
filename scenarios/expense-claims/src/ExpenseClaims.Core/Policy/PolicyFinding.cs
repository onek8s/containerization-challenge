namespace ExpenseClaims.Core.Policy;

public enum FindingSeverity
{
    /// <summary>Shown to the approver; the claim can still be submitted.</summary>
    Warning = 1,

    /// <summary>The claim cannot be submitted until the line is fixed.</summary>
    Error = 2
}

public sealed record PolicyFinding(int LineNumber, FindingSeverity Severity, string Code, string Message);
