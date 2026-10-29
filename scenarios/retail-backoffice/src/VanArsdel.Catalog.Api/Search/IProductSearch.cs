using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VanArsdel.Catalog.Api.Search
{
    public interface IProductSearch
    {
        Task IndexAsync(ProductDocument document, CancellationToken cancellationToken);
        Task IndexManyAsync(IReadOnlyCollection<ProductDocument> documents, CancellationToken cancellationToken);
        Task RemoveAsync(string id, CancellationToken cancellationToken);
        Task<ProductSearchResult> SearchAsync(ProductSearchQuery query, CancellationToken cancellationToken);
        Task<bool> PingAsync(CancellationToken cancellationToken);
    }
}
