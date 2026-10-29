using Proseware.ControlTower.Domain.Customs;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Integration.Customs;

public sealed record CustomsSubmitResult(bool Accepted, string? Mrn, DateTimeOffset? AcceptedAt, IReadOnlyList<string> Errors);

public sealed record CustomsStatusResult(DeclarationStatus Status, string? Mrn, DateTimeOffset? StatusAt, string? Reason);

public interface ICustomsGateway
{
    Task<CustomsSubmitResult> SubmitAsync(CustomsDeclaration declaration, Shipment shipment, CancellationToken cancellationToken);

    Task<CustomsStatusResult> GetStatusAsync(CustomsDeclaration declaration, CancellationToken cancellationToken);
}
