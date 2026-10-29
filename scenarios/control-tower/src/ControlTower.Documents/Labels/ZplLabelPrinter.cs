using System.Globalization;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Documents.Labels;

/// <summary>
/// 4x6" (102 x 152 mm) transport labels in ZPL II for the Zebra ZT410/ZT411 printers at the terminals.
/// Layout follows the GS1 logistic label: sender/receiver block, route block, SSCC as GS1-128.
/// </summary>
public sealed class ZplLabelPrinter : ILabelPrinter
{
    private readonly PrinterOptions _options;
    private readonly ILogger<ZplLabelPrinter> _logger;

    public ZplLabelPrinter(IOptions<PrinterOptions> options, ILogger<ZplLabelPrinter> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string BuildPackageLabel(Shipment shipment, Package package, int index, int count)
    {
        var origin = shipment.Origin;
        var destination = shipment.Destination;
        var firstLeg = shipment.Legs.OrderBy(l => l.Sequence).FirstOrDefault();
        var lastLeg = shipment.Legs.OrderBy(l => l.Sequence).LastOrDefault();
        var routeCode = RouteCode(shipment);
        var sscc = Sscc.Normalize(package.Sscc);

        var z = new StringBuilder(2048);
        z.Append("^XA\n");
        z.Append("^CI28\n");             // UTF-8
        z.Append("^PW812^LL1218^LH0,0\n"); // 4x6" at 203 dpi
        z.Append("^PR4^MD10\n");

        // Sender
        z.Append("^FO30,25^A0N,22,22^FDFROM^FS\n");
        z.Append(Field(30, 50, 26, shipment.ShipperName));
        z.Append(Field(30, 80, 24, origin?.Street));
        z.Append(Field(30, 106, 24, origin is null ? null : $"{origin.PostalCode} {origin.City} {origin.CountryCode}"));

        // Carrier and house number
        z.Append("^FO470,25^A0N,22,22^FDCARRIER^FS\n");
        z.Append(Field(470, 50, 26, firstLeg?.Carrier?.Name ?? "Proseware Freight"));
        z.Append(Field(470, 80, 30, shipment.Number));
        z.Append("^FO20,140^GB772,3,3^FS\n");

        // Receiver
        z.Append("^FO30,155^A0N,22,22^FDTO^FS\n");
        z.Append(Field(30, 182, 40, shipment.ConsigneeName));
        z.Append(Field(30, 228, 34, destination?.Street));
        z.Append(Field(30, 268, 44, destination is null ? null : $"{destination.PostalCode} {destination.City}"));
        z.Append(Field(30, 318, 34, destination?.CountryCode));
        z.Append("^FO20,365^GB772,3,3^FS\n");

        // Route block, reversed
        z.Append("^FO30,380^GB300,150,150^FS\n");
        z.Append($"^FO45,400^A0N,110,90^FR^FD{Escape(routeCode)}^FS\n");
        z.Append(Field(350, 385, 26, $"{TransportModeText(shipment.Mode)} / {shipment.IncotermCode}"));
        z.Append(Field(350, 420, 26, $"PKG {index} / {count}   {package.WeightKg.ToString("0.0", CultureInfo.InvariantCulture)} kg"));
        z.Append(Field(350, 455, 24, $"{package.LengthCm}x{package.WidthCm}x{package.HeightCm} cm  {package.Kind}"));
        z.Append(Field(350, 490, 24, shipment.CustomerReference is null ? null : "REF " + shipment.CustomerReference));
        if (lastLeg?.To is { } hub && lastLeg.To.Id != destination?.Id)
            z.Append(Field(350, 520, 22, "VIA " + hub.Code));

        // Handling marks
        var markY = 550;
        if (shipment.IsReefer)
        {
            z.Append($"^FO30,{markY}^GB360,60,4^FS\n");
            z.Append($"^FO45,{markY + 15}^A0N,34,30^FD{Escape(ReeferText(shipment))}^FS\n");
        }
        if (package.DangerousGoods is { } dg)
        {
            // Hazard diamond: square rotated 45 degrees is not available in ZPL, the class is printed in a box.
            z.Append($"^FO420,{markY - 5}^GB120,120,6^FS\n");
            z.Append($"^FO440,{markY + 25}^A0N,60,50^FD{Escape(dg.HazardClass)}^FS\n");
            z.Append(Field(560, markY, 26, "UN " + dg.UnNumber));
            z.Append(Field(560, markY + 32, 20, Truncate(dg.ProperShippingName, 22)));
            if (dg.LimitedQuantity) z.Append(Field(560, markY + 60, 22, "LTD QTY"));
            if (!string.IsNullOrEmpty(dg.PackingGroup)) z.Append(Field(560, markY + 86, 22, "PG " + dg.PackingGroup));
        }
        if (!package.Stackable)
            z.Append(Field(30, markY + 75, 30, "DO NOT STACK"));

        z.Append("^FO20,700^GB772,3,3^FS\n");

        // SSCC as GS1-128: start code C, FNC1 (>8), AI 00 + 18 digits
        z.Append("^FO30,715^A0N,26,26^FDSSCC^FS\n");
        z.Append($"^FO30,745^A0N,36,32^FD(00) {Group(sscc)}^FS\n");
        z.Append("^BY4,3,260\n");
        z.Append($"^FO60,800^BCN,260,N,N,N,D^FD>;>800{sscc}^FS\n");
        z.Append($"^FO30,1090^A0N,22,22^FDPrinted {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC^FS\n");
        z.Append("^PQ1,0,1,Y\n");
        z.Append("^XZ\n");
        return z.ToString();
    }

    public async Task PrintAsync(string printerName, string zpl, CancellationToken cancellationToken = default)
    {
        if (!_options.Printers.TryGetValue(printerName, out var printer))
            throw new InvalidOperationException($"Printer '{printerName}' is not configured.");

        if (printer.Kind == PrinterKind.File)
        {
            Directory.CreateDirectory(printer.Address);
            var path = Path.Combine(printer.Address, $"{printerName}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.zpl");
            await File.WriteAllTextAsync(path, zpl, new UTF8Encoding(false), cancellationToken);
            _logger.LogInformation("Label for {Printer} written to {Path}", printerName, path);
            return;
        }

        var (host, port) = ParseAddress(printer.Address);
        var payload = Encoding.UTF8.GetBytes(zpl);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(_options.SendTimeoutSeconds));
                using var client = new TcpClient { NoDelay = true, SendTimeout = _options.SendTimeoutSeconds * 1000 };
                await client.ConnectAsync(host, port, timeout.Token);
                await using var stream = client.GetStream();
                await stream.WriteAsync(payload, timeout.Token);
                await stream.FlushAsync(timeout.Token);
                _logger.LogInformation("Sent {Bytes} bytes to printer {Printer} ({Host}:{Port})", payload.Length, printerName, host, port);
                return;
            }
            catch (Exception ex) when (attempt < _options.Retries && ex is SocketException or OperationCanceledException or IOException && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Printer {Printer} not reachable, retrying ({Attempt})", printerName, attempt + 1);
                await Task.Delay(TimeSpan.FromSeconds(2 * (attempt + 1)), cancellationToken);
            }
        }
    }

    public IReadOnlyCollection<string> PrinterNames => _options.Printers.Keys;

    public static (string Host, int Port) ParseAddress(string address)
    {
        var parts = address.Split(':', 2);
        return (parts[0], parts.Length == 2 && int.TryParse(parts[1], out var p) ? p : 9100);
    }

    /// <summary>Destination route code shown in the reversed box: country + first two postcode digits.</summary>
    public static string RouteCode(Shipment shipment)
    {
        var d = shipment.Destination;
        if (d is null) return "??";
        var digits = new string(d.PostalCode.Where(char.IsLetterOrDigit).Take(2).ToArray());
        return (d.CountryCode + digits).ToUpperInvariant();
    }

    private static string ReeferText(Shipment s) =>
        string.Create(CultureInfo.InvariantCulture, $"KEEP {s.TemperatureMin:0.#} - {s.TemperatureMax:0.#} C");

    private static string TransportModeText(TransportMode mode) => mode switch
    {
        TransportMode.Air => "AIR",
        TransportMode.Sea => "SEA",
        TransportMode.Rail => "RAIL",
        _ => "ROAD"
    };

    private static string Field(int x, int y, int size, string? text) =>
        string.IsNullOrWhiteSpace(text) ? "" : $"^FO{x},{y}^A0N,{size},{size}^FD{Escape(Truncate(text, 34))}^FS\n";

    private static string Group(string sscc) =>
        sscc.Length == 18 ? $"{sscc[0]} {sscc.Substring(1, 10)} {sscc.Substring(11, 6)} {sscc[17]}" : sscc;

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

    /// <summary>^ and ~ are ZPL command prefixes and cannot appear in field data.</summary>
    internal static string Escape(string text) => text.Replace('^', ' ').Replace('~', ' ');
}
