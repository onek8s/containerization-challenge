using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ProductReturns.Core
{
    public class ReturnableLine
    {
        public int LineNumber { get; set; }
        public string Sku { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public decimal UnitPrice { get; set; }
        public int OrderedQuantity { get; set; }
        public int AlreadyReturned { get; set; }
        public int ReturnableQuantity { get; set; }
        public DateTime? ReturnByDate { get; set; }
        public string NotReturnableReason { get; set; }
        public bool IsReturnable => ReturnableQuantity > 0;
    }

    /// <summary>Decides which order lines a customer can still return.</summary>
    public class ReturnPolicy
    {
        private readonly ReturnPolicyOptions _options;

        public ReturnPolicy(ReturnPolicyOptions options)
        {
            _options = options ?? new ReturnPolicyOptions();
        }

        public ReturnPolicyOptions Options => _options;

        public DateTime? ReturnByDate(DateTime? deliveredDate, string category)
        {
            if (deliveredDate == null)
            {
                return null;
            }

            var delivered = deliveredDate.Value.Date;
            var days = _options.DefaultWindowDays;
            if (category != null && _options.CategoryWindowDays != null
                && _options.CategoryWindowDays.TryGetValue(category, out var categoryDays))
            {
                days = categoryDays;
            }

            var returnBy = delivered.AddDays(days);
            var holidayEnd = HolidayReturnUntil(delivered);
            if (holidayEnd.HasValue && holidayEnd.Value > returnBy)
            {
                returnBy = holidayEnd.Value;
            }

            return returnBy;
        }

        private DateTime? HolidayReturnUntil(DateTime delivered)
        {
            if (!TryMonthDay(_options.HolidayFrom, delivered.Year, out var from)
                || !TryMonthDay(_options.HolidayTo, delivered.Year, out var to)
                || !TryMonthDay(_options.HolidayReturnUntil, delivered.Year + 1, out var until))
            {
                return null;
            }

            if (delivered >= from && delivered <= to)
            {
                return until;
            }

            return null;
        }

        private static bool TryMonthDay(string value, int year, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return DateTime.TryParseExact(year.ToString("D4", CultureInfo.InvariantCulture) + "-" + value.Trim(),
                "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        /// <summary>
        /// Lists the order lines with the quantity that can still be returned.
        /// </summary>
        /// <param name="order">Order with lines.</param>
        /// <param name="catalog">Catalog lookup by SKU.</param>
        /// <param name="returnedQuantities">Quantity already on open or completed returns, by order line number.</param>
        /// <param name="today">Local business date.</param>
        public IList<ReturnableLine> Evaluate(SalesOrder order, Func<string, CatalogItem> catalog,
            IDictionary<int, int> returnedQuantities, DateTime today)
        {
            if (order == null) throw new ArgumentNullException(nameof(order));

            var result = new List<ReturnableLine>();
            foreach (var line in order.Lines.OrderBy(l => l.LineNumber))
            {
                var item = catalog?.Invoke(line.Sku);
                var category = item?.Category;
                var returned = 0;
                if (returnedQuantities != null)
                {
                    returnedQuantities.TryGetValue(line.LineNumber, out returned);
                }

                var returnable = new ReturnableLine
                {
                    LineNumber = line.LineNumber,
                    Sku = line.Sku,
                    Description = string.IsNullOrEmpty(line.Description) ? item?.Description : line.Description,
                    Category = category,
                    UnitPrice = line.UnitPrice,
                    OrderedQuantity = line.Quantity,
                    AlreadyReturned = returned,
                    ReturnByDate = ReturnByDate(order.DeliveredDate, category)
                };

                returnable.NotReturnableReason = NotReturnableReason(order, line, item, returnable, today);
                returnable.ReturnableQuantity = returnable.NotReturnableReason == null
                    ? Math.Max(0, line.Quantity - returned)
                    : 0;
                if (returnable.ReturnableQuantity == 0 && returnable.NotReturnableReason == null)
                {
                    returnable.NotReturnableReason = "Already returned";
                }

                result.Add(returnable);
            }

            return result;
        }

        private string NotReturnableReason(SalesOrder order, SalesOrderLine line, CatalogItem item,
            ReturnableLine returnable, DateTime today)
        {
            if (line.Quantity <= 0 || line.UnitPrice < 0)
            {
                return "Not a product line";
            }

            if (order.DeliveredDate == null)
            {
                return "Not delivered yet";
            }

            if (item == null)
            {
                return "Unknown product";
            }

            if (!item.Returnable || item.FinalSale)
            {
                return "Final sale";
            }

            if (item.Category != null && _options.NonReturnableCategories != null
                && _options.NonReturnableCategories.Any(c => string.Equals(c, item.Category, StringComparison.OrdinalIgnoreCase)))
            {
                return "This product cannot be returned";
            }

            if (returnable.ReturnByDate.HasValue && today.Date > returnable.ReturnByDate.Value)
            {
                return "Return period ended " + returnable.ReturnByDate.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
            }

            return null;
        }

        public bool ChargesLabelFee(IEnumerable<ReturnLine> lines)
        {
            return lines.Any() && lines.All(l => !ReturnReasons.IsCompanyFault(l.ReasonCode));
        }
    }
}
