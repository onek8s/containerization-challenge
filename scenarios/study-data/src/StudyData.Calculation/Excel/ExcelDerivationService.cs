using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudyData.Core.Services;

namespace StudyData.Calculation.Excel;

[SupportedOSPlatform("windows")]
public class ExcelDerivationService : IDerivationService
{
    private readonly ExcelPool _pool;
    private readonly WorkbookStore _store;
    private readonly CalculationOptions _options;
    private readonly ILogger<ExcelDerivationService> _log;

    public ExcelDerivationService(ExcelPool pool, WorkbookStore store, IOptions<CalculationOptions> options, ILogger<ExcelDerivationService> log)
    {
        _pool = pool;
        _store = store;
        _options = options.Value;
        _log = log;
    }

    public Task<DerivationResult> DeriveAsync(DerivationRequest request, CancellationToken cancellationToken = default)
    {
        var path = _store.GetVerifiedCopy(request.Study, request.Workbook);
        var version = $"{request.Workbook.Code} v{request.Workbook.Version}";
        return _pool.UseAsync(instance =>
        {
            var messages = new List<string>();
            var values = instance.Invoke(() => instance.Calculate(path, request.Inputs, request.Outputs, messages), _options.CalculationTimeout);
            _log.LogDebug("{Study} {Workbook} calculated on Excel #{Number}", request.Study.Code, version, instance.Number);
            return new DerivationResult(values, version, messages);
        }, cancellationToken);
    }
}
