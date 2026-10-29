using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using log4net;
using Wingtip.FieldSales.Api.Infrastructure;
using Wingtip.FieldSales.Contracts;
using Wingtip.FieldSales.Core;
using Wingtip.FieldSales.Data;

namespace Wingtip.FieldSales.Api.Controllers
{
    /// <summary>
    /// Interface for the ERP: the order import job fetches confirmed orders every 15 minutes and acknowledges them
    /// with the ERP order number; the nightly master data job pushes the customer register.
    /// </summary>
    [RoutePrefix("api/erp"), AllowAnonymous, ErpApiKey]
    public class ErpController : ApiController
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ErpController));
        private readonly FieldSalesDbContext _db;
        private readonly SalesProcedures _procedures;

        public ErpController(FieldSalesDbContext db, SalesProcedures procedures)
        {
            _db = db;
            _procedures = procedures;
        }

        [HttpGet, Route("orders")]
        public async Task<List<ErpOrderDto>> PendingOrders(int max = 200)
        {
            var confirmed = (int)OrderStatus.Confirmed;
            var orders = await _db.SalesOrders.AsNoTracking().Include(o => o.Lines)
                .Where(o => o.Status == confirmed && o.ExportedAt == null)
                .OrderBy(o => o.SalesOrderId).Take(System.Math.Min(max, 1000)).ToListAsync();

            return orders.Select(o => new ErpOrderDto
            {
                OrderNo = o.OrderNo,
                RepReference = o.RepReference,
                CustomerNo = o.CustomerNo,
                RepCode = o.RepCode,
                OrderDate = o.CreatedAt.Date,
                RequestedDelivery = o.RequestedDelivery,
                NetTotal = o.NetTotal,
                Comment = o.Comment,
                Lines = o.Lines.OrderBy(l => l.LineNo).Select(l => new ErpOrderLineDto
                {
                    LineNo = l.LineNo, ItemNo = l.ItemNo, Quantity = l.Quantity, UnitPrice = l.UnitPrice,
                    LineDiscountPercent = l.LineDiscountPercent, LineTotal = l.LineTotal
                }).ToList()
            }).ToList();
        }

        [HttpPost, Route("orders/ack")]
        public async Task<IHttpActionResult> Acknowledge(List<ErpAcknowledgement> acknowledgements)
        {
            if (acknowledgements == null) return BadRequest("Body is required.");

            var unknown = new List<string>();
            foreach (var ack in acknowledgements)
            {
                if (!await _procedures.MarkExportedAsync(ack.OrderNo, ack.ErpOrderNo)) unknown.Add(ack.OrderNo);
            }

            Log.InfoFormat("ERP acknowledged {0} orders, {1} unknown or already exported", acknowledgements.Count - unknown.Count, unknown.Count);
            return Ok(new { acknowledged = acknowledgements.Count - unknown.Count, notFound = unknown });
        }

        [HttpPut, Route("customers")]
        public async Task<IHttpActionResult> Customers(List<ErpCustomerDto> customers)
        {
            if (customers == null || customers.Count == 0) return BadRequest("Body is required.");
            var invalid = customers.Where(c => string.IsNullOrWhiteSpace(c.CustomerNo) || string.IsNullOrWhiteSpace(c.Name)).ToList();
            if (invalid.Count > 0) return BadRequest(invalid.Count + " rows have no customer number or name.");

            var result = await _procedures.UpsertCustomersAsync(customers.Select(c => new Customer
            {
                CustomerNo = c.CustomerNo.Trim(), Name = c.Name.Trim(), Street = c.Street, PostalCode = c.PostalCode, City = c.City,
                PriceGroup = string.IsNullOrWhiteSpace(c.PriceGroup) ? "C" : c.PriceGroup.Trim().ToUpperInvariant(),
                CreditBlocked = c.CreditBlocked
            }));

            Log.InfoFormat("ERP customer update: {0} new, {1} updated", result.Inserted, result.Updated);
            return Ok(new ErpCustomerResult { Inserted = result.Inserted, Updated = result.Updated });
        }
    }
}
