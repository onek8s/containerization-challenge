using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VanArsdel.Customers.Api.Documents
{
    public interface ICustomerStore
    {
        Task<CustomerDocument> GetAsync(string id, CancellationToken ct);
        Task<CustomerDocument> FindByEmailAsync(string email, CancellationToken ct);
        Task<CustomerDocument> FindByCardAsync(string cardNo, CancellationToken ct);
        Task<IReadOnlyList<CustomerDocument>> SearchAsync(string text, int take, CancellationToken ct);
        Task<IReadOnlyList<CustomerDocument>> PageAsync(int skip, int take, CancellationToken ct);
        Task<long> CountAsync(CancellationToken ct);

        /// <summary>
        /// Inserts (Version 0) or replaces the document if its version is unchanged; increments Version.
        /// Returns false on a concurrent change.
        /// </summary>
        Task<bool> SaveAsync(CustomerDocument document, CancellationToken ct);

        Task<bool> PingAsync(CancellationToken ct);
    }
}
