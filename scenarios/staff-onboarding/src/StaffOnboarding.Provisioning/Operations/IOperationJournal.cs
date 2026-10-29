using StaffOnboarding.Core.Provisioning;

namespace StaffOnboarding.Provisioning.Operations;

/// <summary>
/// Remembers completed steps per request so a repeated call (the workflow retries after timeouts and
/// host restarts) returns the earlier result instead of running again.
/// </summary>
public interface IOperationJournal
{
    ProvisioningResult? FindCompleted(string requestNumber, string step);
    void Record(string requestNumber, string step, string? samAccountName, string caller, ProvisioningResult result);
}
