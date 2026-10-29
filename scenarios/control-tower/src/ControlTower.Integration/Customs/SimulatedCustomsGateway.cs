using Microsoft.Extensions.Options;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Customs;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Integration.Customs;

/// <summary>Accepts valid declarations, releases them after a few minutes and puts every n-th under control.</summary>
public sealed class SimulatedCustomsGateway : ICustomsGateway
{
    private readonly CustomsOptions _options;
    private readonly IClock _clock;

    public SimulatedCustomsGateway(IOptions<CustomsOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public Task<CustomsSubmitResult> SubmitAsync(CustomsDeclaration declaration, Shipment shipment, CancellationToken cancellationToken)
    {
        var errors = DeclarationValidator.Validate(declaration);
        if (errors.Count > 0) return Task.FromResult(new CustomsSubmitResult(false, null, null, errors));
        var now = _clock.UtcNow;
        var mrn = $"{now:yy}{declaration.OfficeOfDeclaration[..2]}{Math.Abs(declaration.Lrn.GetHashCode()) % 1_000_000_000:D9}{(declaration.Type == DeclarationType.Export ? 'E' : 'I')}{shipment.Id % 100:D2}";
        return Task.FromResult(new CustomsSubmitResult(true, mrn[..Math.Min(18, mrn.Length)].PadRight(18, '0'), now, Array.Empty<string>()));
    }

    public Task<CustomsStatusResult> GetStatusAsync(CustomsDeclaration declaration, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var accepted = declaration.AcceptedAt ?? declaration.SubmittedAt ?? now;
        var underControl = _options.SimulatorControlEvery > 0 && declaration.Id % _options.SimulatorControlEvery == 0;
        if (now - accepted < TimeSpan.FromMinutes(_options.SimulatorReleaseMinutes))
            return Task.FromResult(new CustomsStatusResult(DeclarationStatus.Accepted, declaration.Mrn, accepted, null));
        if (underControl && now - accepted < TimeSpan.FromMinutes(_options.SimulatorReleaseMinutes * 10))
            return Task.FromResult(new CustomsStatusResult(DeclarationStatus.UnderControl, declaration.Mrn, accepted.AddMinutes(_options.SimulatorReleaseMinutes), "Control: documentary check"));
        return Task.FromResult(new CustomsStatusResult(DeclarationStatus.Released, declaration.Mrn, now, null));
    }
}
