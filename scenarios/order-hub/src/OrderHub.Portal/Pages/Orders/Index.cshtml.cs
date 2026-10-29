using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Litware.OrderHub.Core;
using Litware.OrderHub.Core.Channels;
using Litware.OrderHub.Integration.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Litware.OrderHub.Portal.Pages.Orders
{
    public class IndexModel : PageModel
    {
        private readonly IOrderStore _store;

        public IndexModel(IOrderStore store)
        {
            _store = store;
        }

        [BindProperty(SupportsGet = true)] public string Q { get; set; }
        [BindProperty(SupportsGet = true)] public string Status { get; set; }
        [BindProperty(SupportsGet = true)] public string Channel { get; set; }

        public IList<OrderRecord> Orders { get; private set; }

        public IEnumerable<SelectListItem> StatusOptions => new[]
        {
            OrderStatus.Accepted, OrderStatus.Rejected, OrderStatus.SentToWarehouse, OrderStatus.Shipped, OrderStatus.Invoiced
        }.Select(s => new SelectListItem(s, s, s == Status));

        public IEnumerable<SelectListItem> ChannelOptions => new[]
        {
            SalesChannels.Webshop, SalesChannels.Marketplace, SalesChannels.Partner
        }.Select(c => new SelectListItem(c, c, c == Channel));

        public async Task OnGetAsync()
        {
            Orders = await _store.SearchAsync(new OrderQuery { Text = Q, Status = Status, Channel = Channel });
        }
    }
}
