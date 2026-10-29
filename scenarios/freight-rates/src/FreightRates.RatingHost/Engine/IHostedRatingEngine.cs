using FreightRates.Core;

namespace FreightRates.RatingHost.Engine;

public interface IHostedRatingEngine : IRatingEngine
{
    Task<TariffInfo> ReloadTariffsAsync(CancellationToken cancellationToken = default);
    Task<KeyStatus> GetKeyStatusAsync(CancellationToken cancellationToken = default);
}

public sealed record KeyStatus(bool Present, string Serial, int LicenceDaysLeft, int Seats, int SeatsInUse);
