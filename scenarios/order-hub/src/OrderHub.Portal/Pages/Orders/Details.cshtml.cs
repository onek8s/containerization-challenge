using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Litware.OrderHub.Contracts;
using Litware.OrderHub.Core;
using Litware.OrderHub.Integration.Data;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Litware.OrderHub.Portal.Pages.Orders
{
    public class DetailsModel : PageModel
    {
        private readonly IOrderStore _store;
        private readonly IPublishEndpoint _bus;

        public DetailsModel(IOrderStore store, IPublishEndpoint bus)
        {
            _store = store;
            _bus = bus;
        }

        public OrderRecord Order { get; private set; }
        public IList<OrderEvent> Events { get; private set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            Order = await _store.FindAsync(id);
            if (Order == null) return NotFound();
            Events = await _store.GetEventsAsync(id);
            return Page();
        }

        public async Task<IActionResult> OnGetInvoiceAsync(Guid id)
        {
            var order = await _store.FindAsync(id);
            if (order?.InvoiceNumber == null) return NotFound();
            var pdf = await _store.GetInvoicePdfAsync(order.InvoiceNumber);
            if (pdf == null) return NotFound();
            return File(pdf, "application/pdf", order.InvoiceNumber + ".pdf");
        }

        [Authorize(Policy = "Operator")]
        public async Task<IActionResult> OnPostResubmitAsync(Guid id)
        {
            await _bus.Publish<ResubmitOrder>(new { OrderId = id, RequestedBy = User.Identity.Name });
            await _store.AddEventAsync(id, "ResubmitRequested", User.Identity.Name);
            TempData["Message"] = "The order was sent for validation. Refresh in a moment.";
            return RedirectToPage(new { id });
        }

        [Authorize(Policy = "Operator")]
        public async Task<IActionResult> OnPostResendAsync(Guid id)
        {
            await _bus.Publish<ResendToWarehouse>(new { OrderId = id, RequestedBy = User.Identity.Name });
            await _store.AddEventAsync(id, "ResendRequested", User.Identity.Name);
            TempData["Message"] = "The warehouse file will be sent again.";
            return RedirectToPage(new { id });
        }
    }
}
