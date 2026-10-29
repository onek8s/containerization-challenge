using System;
using System.Collections.Generic;
using System.Runtime.Caching;
using System.Web.Http;
using Contoso.Purchasing.Api.Infrastructure;
using Contoso.Purchasing.Core;
using Contoso.Purchasing.Data;

namespace Contoso.Purchasing.Api.Controllers
{
    [RoutePrefix("api/catalog")]
    public class CatalogController : PurchasingController
    {
        [HttpGet, Route("suppliers")]
        public IList<Supplier> Suppliers()
        {
            return Cached("suppliers", () => new CatalogRepository(Db).Suppliers(true));
        }

        [HttpGet, Route("costcenters")]
        public IList<CostCenter> CostCenters()
        {
            return Cached("costcenters", () => new CatalogRepository(Db).CostCenters());
        }

        [HttpGet, Route("categories")]
        public IEnumerable<object> Categories()
        {
            foreach (var code in RequestCategory.All)
            {
                yield return new { code, name = RequestCategory.DisplayName(code) };
            }
        }

        private static T Cached<T>(string key, Func<T> load) where T : class
        {
            var cache = MemoryCache.Default;
            var value = cache.Get("catalog:" + key) as T;
            if (value == null)
            {
                value = load();
                cache.Set("catalog:" + key, value, DateTimeOffset.Now.AddMinutes(15));
            }
            return value;
        }
    }
}
