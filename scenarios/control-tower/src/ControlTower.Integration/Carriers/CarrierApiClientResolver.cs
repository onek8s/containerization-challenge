using Microsoft.Extensions.Options;
using Proseware.ControlTower.Domain.Parties;

namespace Proseware.ControlTower.Integration.Carriers;

public interface ICarrierApiClientResolver
{
    /// <summary>Client for the carrier, the simulator when so configured, or null when disabled / not an API carrier.</summary>
    ICarrierApiClient? Resolve(Carrier carrier);
}

public sealed class CarrierApiClientResolver : ICarrierApiClientResolver
{
    private readonly IEnumerable<ICarrierApiClient> _clients;
    private readonly SimulatedCarrierClient _simulator;
    private readonly CarrierIntegrationOptions _options;

    public CarrierApiClientResolver(IEnumerable<ICarrierApiClient> clients, SimulatedCarrierClient simulator, IOptions<CarrierIntegrationOptions> options)
    {
        _clients = clients;
        _simulator = simulator;
        _options = options.Value;
    }

    public ICarrierApiClient? Resolve(Carrier carrier)
    {
        var settings = _options.ApiFor(carrier.Code);
        if (settings.IsDisabled) return null;
        if (settings.IsSimulator) return _simulator;
        return _clients.FirstOrDefault(c => c.CarrierCodes.Contains(carrier.Code, StringComparer.OrdinalIgnoreCase));
    }
}
