using System;
using System.Collections.Generic;
using System.Linq;

namespace Wingtip.FieldSales.Core
{
    public class OrderValidator
    {
        private readonly DeliveryCalendar _calendar;

        public OrderValidator(DeliveryCalendar calendar)
        {
            _calendar = calendar;
        }

        /// <summary>Returns the problems found. An empty list means the order can be submitted.</summary>
        /// <param name="receivedAt">When the order reached the office. For offline orders this is the sync time,
        /// not the time the rep wrote the order.</param>
        public IList<string> Validate(SalesOrder order, Customer customer, IDictionary<string, Product> products, DateTime receivedAt)
        {
            var errors = new List<string>();

            if (customer == null)
            {
                errors.Add("Unknown customer " + order.CustomerNo + ".");
                return errors;
            }

            if (customer.CreditBlocked) errors.Add("Customer " + customer.CustomerNo + " is blocked by credit control.");
            if (order.Lines.Count == 0) errors.Add("The order has no lines.");

            if (!_calendar.IsValidDelivery(receivedAt, customer.DeliveryDays, order.RequestedDelivery))
            {
                errors.Add(string.Format("Delivery {0:d} is not possible. Next route day is {1:d}.",
                    order.RequestedDelivery, _calendar.NextRouteDay(receivedAt, customer.DeliveryDays)));
            }

            foreach (var duplicate in order.Lines.GroupBy(l => l.ItemNo, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                errors.Add("Item " + duplicate.Key + " is on more than one line.");

            foreach (var line in order.Lines)
            {
                Product product;
                if (!products.TryGetValue(line.ItemNo, out product) || !product.Active)
                {
                    errors.Add("Item " + line.ItemNo + " is not sold any more.");
                    continue;
                }

                if (line.Quantity <= 0) errors.Add("Item " + line.ItemNo + ": quantity must be positive.");
                else if (line.Quantity < product.MinOrderQty)
                    errors.Add(string.Format("Item {0}: minimum order quantity is {1} {2}.", line.ItemNo, product.MinOrderQty, product.Unit));

                if (line.LineDiscountPercent < 0m) errors.Add("Item " + line.ItemNo + ": discount cannot be negative.");
            }

            return errors;
        }
    }
}
