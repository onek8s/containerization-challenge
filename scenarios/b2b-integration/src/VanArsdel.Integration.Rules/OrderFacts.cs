using System;
using System.Globalization;
using System.Linq;
using VanArsdel.Integration.Contracts.Canonical;

namespace VanArsdel.Integration.Rules
{
    /// <summary>
    /// .NET fact wrapping the canonical order for the VanArsdel.OrderValidation policy. The vocabulary
    /// "VanArsdel Orders" exposes these members to the business users who maintain the rules in the composer.
    /// </summary>
    [Serializable]
    public class OrderFacts
    {
        private readonly Order _order;

        public OrderFacts(Order order)
        {
            if (order == null || order.Header == null)
            {
                throw new ArgumentNullException("order");
            }

            _order = order;
        }

        public Order Order
        {
            get { return _order; }
        }

        public string PartnerId
        {
            get { return _order.Header.PartnerId; }
        }

        public string OrderNumber
        {
            get { return _order.Header.OrderNumber; }
        }

        public string Kind
        {
            get { return _order.Header.Kind; }
        }

        public string OrderType
        {
            get { return _order.Header.OrderType ?? string.Empty; }
        }

        public string Currency
        {
            get { return _order.Header.Currency; }
        }

        public int LineCount
        {
            get { return _order.Lines == null ? 0 : _order.Lines.Length; }
        }

        public decimal GetOrderValue()
        {
            if (_order.Lines == null)
            {
                return 0m;
            }

            return _order.Lines.Sum(l => Domain.Orders.Totals.LineAmount(l.Quantity, l.UnitPriceSpecified ? l.UnitPrice : 0m, l.PriceBasisSpecified ? l.PriceBasis : 1m));
        }

        public int DaysUntilRequestedDelivery(DateTime today)
        {
            if (!_order.Header.RequestedDeliveryDateSpecified)
            {
                return 999;
            }

            return (int)(_order.Header.RequestedDeliveryDate.Date - today.Date).TotalDays;
        }

        public bool HasRequestedDelivery()
        {
            return _order.Header.RequestedDeliveryDateSpecified;
        }

        public bool HasZeroQuantityLine()
        {
            return _order.Lines != null && _order.Lines.Any(l => l.Quantity <= 0);
        }

        public int FirstZeroQuantityLine()
        {
            var l = _order.Lines == null ? null : _order.Lines.FirstOrDefault(x => x.Quantity <= 0);
            return l == null ? 0 : l.LineNumber;
        }

        /// <summary>Items the map could not cross-reference start with "?".</summary>
        public bool HasUnresolvedItem()
        {
            return _order.Lines != null && _order.Lines.Any(l => string.IsNullOrEmpty(l.Item) || l.Item.StartsWith("?", StringComparison.Ordinal));
        }

        public string UnresolvedItems()
        {
            if (_order.Lines == null)
            {
                return string.Empty;
            }

            return string.Join(", ", _order.Lines.Where(l => string.IsNullOrEmpty(l.Item) || l.Item.StartsWith("?", StringComparison.Ordinal))
                .Select(l => l.LineNumber.ToString(CultureInfo.InvariantCulture) + "=" + (l.PartnerItem ?? l.Gtin ?? "(none)")));
        }

        public bool HasUnresolvedShipTo()
        {
            var st = ShipTo();
            return st == null || string.IsNullOrEmpty(st.InternalId) || st.InternalId.StartsWith("?", StringComparison.Ordinal);
        }

        public string ShipToCountry()
        {
            var st = ShipTo();
            return st == null ? string.Empty : (st.Country ?? string.Empty);
        }

        /// <summary>Hazardous goods (lithium batteries, solvents, gas cartridges) have material numbers starting with "HZ-".</summary>
        public bool ContainsHazardousGoods()
        {
            return _order.Lines != null && _order.Lines.Any(l => (l.Item ?? string.Empty).StartsWith("HZ-", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Cable sold by the metre: drums over 500 m go from the cable cutting centre.</summary>
        public bool ContainsCableDrum()
        {
            return _order.Lines != null && _order.Lines.Any(l => l.Uom == "M" && l.Quantity >= 500m);
        }

        public decimal MaxLineQuantity()
        {
            return _order.Lines == null || _order.Lines.Length == 0 ? 0m : _order.Lines.Max(l => l.Quantity);
        }

        public bool HasDuplicateLineNumbers()
        {
            return _order.Lines != null && _order.Lines.GroupBy(l => l.LineNumber).Any(g => g.Count() > 1);
        }

        public void SetShipVia(string shipVia)
        {
            _order.Header.ShipVia = shipVia;
        }

        public void SetOrderType(string orderType)
        {
            _order.Header.OrderType = orderType;
        }

        private Party ShipTo()
        {
            return _order.Parties == null ? null : _order.Parties.FirstOrDefault(p => p.Role == "ST");
        }
    }
}
