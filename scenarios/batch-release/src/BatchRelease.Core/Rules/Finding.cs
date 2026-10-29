namespace BatchRelease.Core.Rules;

public enum FindingSeverity
{
    Information = 0,
    Warning = 1,
    Blocking = 2,
}

public sealed record Finding(string Code, FindingSeverity Severity, string Message)
{
    public static Finding Block(string code, string message) => new(code, FindingSeverity.Blocking, message);
    public static Finding Warn(string code, string message) => new(code, FindingSeverity.Warning, message);
    public static Finding Info(string code, string message) => new(code, FindingSeverity.Information, message);
}

public sealed class Assessment
{
    public Assessment(IReadOnlyList<Finding> findings) => Findings = findings;

    public IReadOnlyList<Finding> Findings { get; }

    public bool CanProceed => Findings.All(f => f.Severity != FindingSeverity.Blocking);

    public IEnumerable<Finding> Blocking => Findings.Where(f => f.Severity == FindingSeverity.Blocking);
}
