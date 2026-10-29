using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Litware.OrderHub.Core;
using Litware.OrderHub.Integration.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Litware.OrderHub.Portal.Pages
{
    public class IndexModel : PageModel
    {
        public static readonly string[] Statuses =
        {
            OrderStatus.Accepted, OrderStatus.SentToWarehouse, OrderStatus.Shipped, OrderStatus.Invoiced, OrderStatus.Rejected
        };

        private readonly IOrderStore _store;

        public IndexModel(IOrderStore store)
        {
            _store = store;
        }

        public IDictionary<string, int> Counts { get; private set; }
        public IList<OrderRecord> Rejected { get; private set; }

        public async Task OnGetAsync()
        {
            Counts = await _store.CountByStatusAsync(DateTime.UtcNow.AddDays(-1));
            Rejected = await _store.SearchAsync(new OrderQuery { Status = OrderStatus.Rejected, Top = 20 });
        }
    }
}
