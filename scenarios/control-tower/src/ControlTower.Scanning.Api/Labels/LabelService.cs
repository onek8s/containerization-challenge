using Proseware.ControlTower.Data;
using Proseware.ControlTower.Documents;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Scanning.Labels;

public sealed class LabelService
{
    private readonly ShipmentQueries _queries;
    private readonly ILabelPrinter _printer;
    private readonly ILogger<LabelService> _logger;

    public LabelService(ShipmentQueries queries, ILabelPrinter printer, ILogger<LabelService> logger)
    {
        _queries = queries;
        _printer = printer;
        _logger = logger;
    }

    /// <summary>Prints all labels of a shipment, or the one package when <paramref name="packageId"/> is given.</summary>
    public async Task<int> PrintAsync(Shipment shipment, int? packageId, string printerName, string requestedBy, CancellationToken ct)
    {
        var packages = shipment.Packages.OrderBy(p => p.Id).ToList();
        var printed = 0;
        for (var i = 0; i < packages.Count; i++)
        {
            var package = packages[i];
            if (packageId is { } only && package.Id != only) continue;
            for (var copy = 0; copy < package.Quantity; copy++)
            {
                var zpl = _printer.BuildPackageLabel(shipment, package, i + 1, packages.Count);
                await _printer.PrintAsync(printerName, zpl, ct);
                printed++;
            }
        }
        _logger.LogInformation("{Count} labels of {Shipment} printed on {Printer} for {User}", printed, shipment.Number, printerName, requestedBy);
        return printed;
    }

    public async Task<(int Printed, string? Error)> PrintAsync(string? shipmentNumber, string? sscc, string printerName, string requestedBy, CancellationToken ct)
    {
        Shipment? shipment = null;
        int? packageId = null;
        if (!string.IsNullOrWhiteSpace(shipmentNumber))
        {
            shipment = await _queries.GetByNumberAsync(shipmentNumber.Trim(), ct);
        }
        else if (!string.IsNullOrWhiteSpace(sscc))
        {
            var normalized = Sscc.Normalize(sscc);
            shipment = await _queries.FindForStatusAsync(null, null, normalized, ct);
            packageId = shipment?.Packages.FirstOrDefault(p => p.Sscc == normalized)?.Id;
        }
        if (shipment is null) return (0, "Shipment or package not found.");
        return (await PrintAsync(shipment, packageId, printerName, requestedBy, ct), null);
    }
}
