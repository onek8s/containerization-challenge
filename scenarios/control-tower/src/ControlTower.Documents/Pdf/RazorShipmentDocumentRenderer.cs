using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proseware.ControlTower.Domain.Documents;
using Proseware.ControlTower.Domain.Shipments;
using RazorLight;

namespace Proseware.ControlTower.Documents.Pdf;

/// <summary>Shipment documents from the embedded Razor templates in /Templates, printed to PDF.</summary>
public sealed class RazorShipmentDocumentRenderer : IShipmentDocumentRenderer
{
    private static readonly Dictionary<DocumentKind, string> Templates = new()
    {
        [DocumentKind.Cmr] = "Cmr",
        [DocumentKind.HouseBillOfLading] = "HouseBillOfLading",
        [DocumentKind.HouseAirWaybill] = "HouseAirWaybill",
        [DocumentKind.DeliveryNote] = "DeliveryNote",
        [DocumentKind.DangerousGoodsNote] = "DangerousGoodsNote",
    };

    private readonly RazorLightEngine _engine;
    private readonly IHtmlToPdfConverter _pdf;
    private readonly DocumentOptions _options;
    private readonly ILogger<RazorShipmentDocumentRenderer> _logger;

    public RazorShipmentDocumentRenderer(IHtmlToPdfConverter pdf, IOptions<DocumentOptions> options, ILogger<RazorShipmentDocumentRenderer> logger)
    {
        _pdf = pdf;
        _options = options.Value;
        _logger = logger;
        _engine = CreateEngine();
    }

    public static RazorLightEngine CreateEngine() =>
        new RazorLightEngineBuilder()
            .UseEmbeddedResourcesProject(typeof(RazorShipmentDocumentRenderer).Assembly, "Proseware.ControlTower.Documents.Templates")
            .SetOperatingAssembly(typeof(RazorShipmentDocumentRenderer).Assembly)
            .UseMemoryCachingProvider()
            .Build();

    public bool Supports(DocumentKind kind) => Templates.ContainsKey(kind);

    public async Task<string> RenderHtmlAsync(Shipment shipment, DocumentKind kind, DateTimeOffset issuedAt)
    {
        if (!Templates.TryGetValue(kind, out var template))
            throw new NotSupportedException($"No template for {kind}.");
        var model = new ShipmentDocumentModel(shipment, kind, _options.Issuer, issuedAt, _options.TrackingUrl.Replace("{number}", shipment.Number, StringComparison.Ordinal));
        return await _engine.CompileRenderAsync(template, model);
    }

    public async Task<byte[]> RenderPdfAsync(Shipment shipment, DocumentKind kind, CancellationToken cancellationToken = default)
    {
        var html = await RenderHtmlAsync(shipment, kind, DateTimeOffset.UtcNow);
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("Rendering {Kind} for {Shipment}", kind, shipment.Number);
        return _pdf.Convert(html, $"{kind} {shipment.Number}", landscape: false);
    }
}
