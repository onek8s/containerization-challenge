using System.Collections.Generic;
using System.Threading.Tasks;
using Litware.OrderHub.Core;
using Litware.OrderHub.Integration.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serilog;

namespace Litware.OrderHub.Portal.Pages.Products
{
    public class IndexModel : PageModel
    {
        private readonly IOrderStore _store;

        public IndexModel(IOrderStore store)
        {
            _store = store;
        }

        public IList<Product> Products { get; private set; }

        public async Task OnGetAsync()
        {
            Products = await _store.GetProductsAsync();
        }

        public async Task<IActionResult> OnPostToggleAsync(string sku)
        {
            var product = await _store.FindProductAsync(sku);
            if (product == null) return NotFound();
            await _store.SetProductActiveAsync(sku, !product.Active);
            Log.Information("{User} set product {Sku} active={Active}", User.Identity.Name, sku, !product.Active);
            return RedirectToPage();
        }
    }
}
