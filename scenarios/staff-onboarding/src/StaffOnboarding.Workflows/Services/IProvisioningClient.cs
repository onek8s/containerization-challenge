using System.Threading.Tasks;
using StaffOnboarding.Core.Provisioning;

namespace StaffOnboarding.Workflows.Services
{
    public interface IProvisioningClient
    {
        Task<ProvisioningResult> CreateAccountAsync(CreateAccountCommand command);
        Task<ProvisioningResult> EnableAccountAsync(EnableAccountCommand command);
        Task<ProvisioningResult> UpdateAccessAsync(UpdateAccessCommand command);
        Task<ProvisioningResult> DisableAccountAsync(DisableAccountCommand command);
        Task<ProvisioningResult> ArchiveAccountAsync(ArchiveAccountCommand command);

        /// <summary>Returns null when the account does not exist.</summary>
        Task<AccountSummary> GetAccountAsync(string samAccountName);
    }
}
