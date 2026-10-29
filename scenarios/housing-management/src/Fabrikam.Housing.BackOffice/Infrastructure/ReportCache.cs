using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace Fabrikam.Housing.BackOffice.Infrastructure
{
    /// <summary>
    /// Generated report workbooks are kept in memory for ten minutes; the month end packs are
    /// downloaded by several people in the finance team within a few minutes of each other.
    /// </summary>
    public class ReportCache
    {
        private readonly IMemoryCache _cache;

        public ReportCache(IMemoryCache cache)
        {
            _cache = cache;
        }

        public Task<byte[]> GetOrCreateAsync(string key, Func<Task<byte[]>> factory)
        {
            return _cache.GetOrCreateAsync("report:" + key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                entry.Size = null;
                return factory();
            });
        }
    }
}
