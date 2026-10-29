using StudyData.Core.Design;

namespace StudyData.Core.Services;

public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public record DerivationRequest(Study Study, CalculationWorkbook Workbook, IReadOnlyDictionary<string, object?> Inputs, IReadOnlyCollection<string> Outputs);

public record DerivationResult(IReadOnlyDictionary<string, string?> Values, string WorkbookVersion, IReadOnlyList<string> Messages);

/// <summary>Evaluates the validated calculation workbook of a study.</summary>
public interface IDerivationService
{
    Task<DerivationResult> DeriveAsync(DerivationRequest request, CancellationToken cancellationToken = default);
}

public class DerivationException : Exception
{
    public DerivationException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}
