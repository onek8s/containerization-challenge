using StudyData.Core.Services;

namespace StudyData.Monitor.Api.Services;

public sealed class NoDerivationService : IDerivationService
{
    public Task<DerivationResult> DeriveAsync(DerivationRequest request, CancellationToken cancellationToken = default) =>
        throw new DerivationException("Data entry is done in the site portal.");
}
