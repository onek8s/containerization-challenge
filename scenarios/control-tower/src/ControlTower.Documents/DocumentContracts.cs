using Proseware.ControlTower.Domain.Documents;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Documents;

/// <summary>Stores document content; metadata is in ct.Document.</summary>
public interface IDocumentStore
{
    Task<StoredDocument> SaveAsync(StoredDocument metadata, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Opens the content for reading. The caller disposes the stream.</summary>
    Task<Stream?> OpenAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid documentId, CancellationToken cancellationToken = default);
}

/// <summary>Renders shipment documents (CMR, house bill of lading, air waybill, delivery note...) as PDF.</summary>
public interface IShipmentDocumentRenderer
{
    bool Supports(DocumentKind kind);

    Task<byte[]> RenderPdfAsync(Shipment shipment, DocumentKind kind, CancellationToken cancellationToken = default);
}

/// <summary>Renders and stores a document for a shipment.</summary>
public interface IShipmentDocumentService
{
    Task<StoredDocument> CreateAsync(int shipmentId, DocumentKind kind, string requestedBy, CancellationToken cancellationToken = default);
}

/// <summary>Builds ZPL for package labels and sends it to the label printers.</summary>
public interface ILabelPrinter
{
    string BuildPackageLabel(Shipment shipment, Package package, int index, int count);

    Task PrintAsync(string printerName, string zpl, CancellationToken cancellationToken = default);
}
