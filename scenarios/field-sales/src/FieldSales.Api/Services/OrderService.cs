using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web.Hosting;
using log4net;
using Wingtip.FieldSales.Api.Infrastructure;
using Wingtip.FieldSales.Contracts;
using Wingtip.FieldSales.Core;
using Wingtip.FieldSales.Data;
using Wingtip.FieldSales.Data.Mail;

namespace Wingtip.FieldSales.Api.Services
{
    public class OrderService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(OrderService));

        private readonly FieldSalesDbContext _db;
        private readonly SalesProcedures _procedures;
        private readonly CatalogService _catalog;

        public OrderService(FieldSalesDbContext db, SalesProcedures procedures, CatalogService catalog)
        {
            _db = db;
            _procedures = procedures;
            _catalog = catalog;
        }

        public async Task<OrderSubmitResponse> SubmitAsync(OrderSubmitRequest request, IPrincipal user, string clientVersion)
        {
            var existing = await _db.SalesOrders.AsNoTracking().FirstOrDefaultAsync(o => o.ClientOrderId == request.ClientOrderId);
            if (existing != null)
            {
                // The client did not get the previous answer (connection dropped on the road) and sends the order again.
                return new OrderSubmitResponse
                {
                    Result = SubmitResults.AlreadyReceived,
                    OrderNo = existing.OrderNo,
                    Status = ((OrderStatus)existing.Status).ToString(),
                    NetTotal = existing.NetTotal
                };
            }

            var customerEntity = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerNo == request.CustomerNo);
            var customer = customerEntity == null ? null : customerEntity.ToCore();
            var repCode = user.RepCode();
            var isManager = user.IsInRole(Roles.SalesManager);

            if (customer != null && !isManager && !string.Equals(customer.RepCode, repCode, StringComparison.OrdinalIgnoreCase))
            {
                return Invalid("Customer " + customer.CustomerNo + " is not assigned to you.");
            }

            var pricing = await _catalog.LoadPricingAsync();
            var order = ToOrder(request, repCode, pricing.Products);

            var errors = new OrderValidator(pricing.Calendar).Validate(order, customer, pricing.Products, DateTime.Now);
            if (errors.Count > 0) return new OrderSubmitResponse { Result = SubmitResults.Invalid, Errors = errors.ToList() };

            var differences = PriceCheck.Compare(order, customer, pricing.Products, pricing.Calculator);
            if (differences.Count > 0)
            {
                Log.InfoFormat("Order {0} from {1} has {2} changed prices (client price list {3})",
                    request.RepReference, user.Identity.Name, differences.Count, request.PriceListVersion);
                return new OrderSubmitResponse
                {
                    Result = SubmitResults.PriceChanged,
                    PriceDifferences = differences.Select(d => new PriceDifferenceDto
                    {
                        ItemNo = d.ItemNo, OrderedPrice = d.OrderedPrice, CurrentPrice = d.CurrentPrice
                    }).ToList()
                };
            }

            var decision = new DiscountPolicy().Evaluate(order, user.DiscountLimit());
            if (decision == DiscountDecision.NotAllowed)
            {
                return Invalid("Line discounts above " + DiscountPolicy.CompanyMaximumPercent + " % are not allowed.");
            }

            var status = decision == DiscountDecision.NeedsApproval ? OrderStatus.PendingApproval : OrderStatus.Confirmed;
            var result = await _procedures.SubmitOrderAsync(order, status, user.Identity.Name, clientVersion, pricing.Products);

            Log.InfoFormat("Order {0} ({1}) for {2} received from {3}: {4}, {5:0.00} EUR",
                result.OrderNo, order.RepReference, order.CustomerNo, user.Identity.Name, (OrderStatus)result.Status, order.NetTotal);

            if (!result.AlreadyReceived && result.Status == (int)OrderStatus.Confirmed) QueueConfirmation(result.SalesOrderId);

            return new OrderSubmitResponse
            {
                Result = result.AlreadyReceived ? SubmitResults.AlreadyReceived : SubmitResults.Accepted,
                OrderNo = result.OrderNo,
                Status = ((OrderStatus)result.Status).ToString(),
                NetTotal = order.NetTotal
            };
        }

        public async Task<List<OrderSummaryDto>> RecentAsync(IPrincipal user, DateTime since)
        {
            IQueryable<Data.Entities.SalesOrderEntity> query = _db.SalesOrders.AsNoTracking().Include(o => o.Customer).Where(o => o.ReceivedAt >= since);
            if (!user.IsInRole(Roles.SalesManager))
            {
                var name = user.Identity.Name;
                query = query.Where(o => o.SubmittedBy == name);
            }

            var orders = await query.OrderByDescending(o => o.ReceivedAt).Take(500).ToListAsync();
            return orders.Select(o => new OrderSummaryDto
            {
                ClientOrderId = o.ClientOrderId,
                OrderNo = o.OrderNo,
                RepReference = o.RepReference,
                CustomerNo = o.CustomerNo,
                CustomerName = o.Customer.Name,
                CreatedAt = o.CreatedAt,
                RequestedDelivery = o.RequestedDelivery,
                Status = ((OrderStatus)o.Status).ToString(),
                NetTotal = o.NetTotal,
                DecisionComment = o.DecisionComment
            }).ToList();
        }

        /// <summary>The confirmation mail is sent after the response so that a slow SMTP relay does not delay the sync.</summary>
        private static void QueueConfirmation(int salesOrderId)
        {
            HostingEnvironment.QueueBackgroundWorkItem(async cancellationToken =>
            {
                try
                {
                    using (var db = new FieldSalesDbContext())
                    {
                        await new ConfirmationMailer(db, MailSettings.FromConfig()).SendAsync(salesOrderId);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Order confirmation mail for order id " + salesOrderId + " failed", ex);
                }
            });
        }

        private static SalesOrder ToOrder(OrderSubmitRequest request, string repCode, IDictionary<string, Product> products)
        {
            var order = new SalesOrder
            {
                ClientOrderId = request.ClientOrderId,
                RepReference = request.RepReference,
                CustomerNo = request.CustomerNo,
                RepCode = repCode,
                CreatedAt = request.CreatedAt,
                RequestedDelivery = request.RequestedDelivery.Date,
                Comment = request.Comment
            };

            var lineNo = 0;
            foreach (var l in request.Lines ?? new List<OrderLineDto>())
            {
                Product product;
                products.TryGetValue(l.ItemNo ?? "", out product);
                order.Lines.Add(new OrderLine
                {
                    LineNo = ++lineNo,
                    ItemNo = product != null ? product.ItemNo : l.ItemNo,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    LineDiscountPercent = l.LineDiscountPercent,
                    VatRate = product != null ? product.VatRate : 0m
                });
            }
            return order;
        }

        private static OrderSubmitResponse Invalid(string message)
        {
            return new OrderSubmitResponse { Result = SubmitResults.Invalid, Errors = new List<string> { message } };
        }
    }
}
