using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Wingtip.FieldSales.Client.Services;
using Wingtip.FieldSales.Contracts;
using Wingtip.FieldSales.Core;

namespace Wingtip.FieldSales.Client.ViewModels
{
    public class OrderLineViewModel : ViewModelBase
    {
        private readonly Action _changed;
        private int _quantity;
        private decimal _discount;

        public OrderLineViewModel(Product product, decimal unitPrice, Action changed)
        {
            Product = product;
            UnitPrice = unitPrice;
            _quantity = Math.Max(1, product.MinOrderQty);
            _changed = changed;
        }

        public Product Product { get; private set; }
        public decimal UnitPrice { get; private set; }

        public int Quantity
        {
            get { return _quantity; }
            set { if (Set(ref _quantity, value)) Changed(); }
        }

        public decimal LineDiscountPercent
        {
            get { return _discount; }
            set { if (Set(ref _discount, value)) Changed(); }
        }

        public decimal LineTotal
        {
            get { return ToLine(0).LineTotal; }
        }

        public void Reprice(decimal unitPrice)
        {
            UnitPrice = unitPrice;
            OnPropertyChanged("UnitPrice");
            Changed();
        }

        public OrderLine ToLine(int lineNo)
        {
            return new OrderLine
            {
                LineNo = lineNo, ItemNo = Product.ItemNo, Quantity = Quantity, UnitPrice = UnitPrice,
                LineDiscountPercent = LineDiscountPercent, VatRate = Product.VatRate
            };
        }

        private void Changed()
        {
            OnPropertyChanged("LineTotal");
            _changed();
        }
    }

    /// <summary>Order entry at the customer's store. Works offline with the cached price list.</summary>
    public class OrderEntryViewModel : ViewModelBase
    {
        private readonly LocalStore _store;
        private readonly Func<PricingService> _pricing;
        private readonly Func<string> _repCode;
        private readonly Func<decimal> _discountLimit;
        private readonly ApiClient _api;
        private string _stockText;
        private CustomerDto _customer;
        private DateTime? _delivery;
        private string _comment;
        private string _search;
        private Product _selectedProduct;
        private string _message;
        private Guid _clientOrderId = Guid.NewGuid();
        private string _repReference;

        public OrderEntryViewModel(LocalStore store, Func<PricingService> pricing, Func<string> repCode, Func<decimal> discountLimit, ApiClient api)
        {
            _api = api;
            _store = store;
            _pricing = pricing;
            _repCode = repCode;
            _discountLimit = discountLimit;
            Lines = new ObservableCollection<OrderLineViewModel>();
            Customers = new ObservableCollection<CustomerDto>();
            SearchResults = new ObservableCollection<Product>();

            AddLineCommand = new RelayCommand(_ => AddLine(), _ => SelectedProduct != null && Customer != null);
            RemoveLineCommand = new RelayCommand(l => { Lines.Remove((OrderLineViewModel)l); Recalculate(); });
            SaveCommand = new RelayCommand(_ => Save(), _ => Customer != null && Lines.Count > 0 && Delivery.HasValue);
            ClearCommand = new RelayCommand(_ => Clear());
        }

        public event EventHandler Saved;

        public ObservableCollection<CustomerDto> Customers { get; private set; }
        public ObservableCollection<Product> SearchResults { get; private set; }
        public ObservableCollection<OrderLineViewModel> Lines { get; private set; }

        public RelayCommand AddLineCommand { get; private set; }
        public RelayCommand RemoveLineCommand { get; private set; }
        public RelayCommand SaveCommand { get; private set; }
        public RelayCommand ClearCommand { get; private set; }

        public CustomerDto Customer
        {
            get { return _customer; }
            set
            {
                if (!Set(ref _customer, value)) return;
                if (value != null)
                {
                    var pricing = _pricing();
                    var route = (DeliveryDays)value.DeliveryDays;
                    Delivery = route == DeliveryDays.None ? (DateTime?)null : pricing.Calendar.NextRouteDay(DateTime.Now, route);
                    foreach (var line in Lines) line.Reprice(pricing.Calculator.UnitPrice(line.Product, value.PriceGroup, Delivery ?? DateTime.Today));
                    Message = value.CreditBlocked ? "The customer is blocked by credit control. The office will not accept the order." : null;
                }
                OnPropertyChanged("RouteText");
            }
        }

        public string RouteText
        {
            get { return Customer == null ? "" : "Route days: " + ((DeliveryDays)Customer.DeliveryDays).ToShortText(); }
        }

        public DateTime? Delivery
        {
            get { return _delivery; }
            set
            {
                if (!Set(ref _delivery, value) || Customer == null || !value.HasValue) return;
                var pricing = _pricing();
                foreach (var line in Lines) line.Reprice(pricing.Calculator.UnitPrice(line.Product, Customer.PriceGroup, value.Value));
            }
        }

        public string Comment
        {
            get { return _comment; }
            set { Set(ref _comment, value); }
        }

        public string Search
        {
            get { return _search; }
            set
            {
                if (!Set(ref _search, value)) return;
                SearchResults.Clear();
                if (string.IsNullOrWhiteSpace(value) || value.Length < 2) return;
                foreach (var p in _pricing().Products.Values
                    .Where(p => p.Active && (p.ItemNo.StartsWith(value, StringComparison.OrdinalIgnoreCase)
                        || p.Name.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) >= 0))
                    .OrderBy(p => p.Name).Take(40))
                    SearchResults.Add(p);
                SelectedProduct = SearchResults.FirstOrDefault();
            }
        }

        public Product SelectedProduct
        {
            get { return _selectedProduct; }
            set
            {
                if (!Set(ref _selectedProduct, value)) return;
                StockText = null;
                if (value != null) LoadStock(value.ItemNo);
            }
        }

        /// <summary>Warehouse stock for the selected product; only shown when the office is reachable.</summary>
        public string StockText
        {
            get { return _stockText; }
            set { Set(ref _stockText, value); }
        }

        public string Message
        {
            get { return _message; }
            set { Set(ref _message, value); }
        }

        public decimal NetTotal { get { return BuildOrder().NetTotal; } }
        public decimal VatTotal { get { return BuildOrder().VatTotal; } }
        public decimal GrossTotal { get { return BuildOrder().GrossTotal; } }

        public string ApprovalHint
        {
            get
            {
                var decision = new DiscountPolicy().Evaluate(BuildOrder(), _discountLimit());
                if (decision == DiscountDecision.NotAllowed) return "Line discount above " + DiscountPolicy.CompanyMaximumPercent + " % is not allowed.";
                if (decision == DiscountDecision.NeedsApproval) return "Discount is above your limit (" + _discountLimit() + " %). The order goes to the sales manager for approval.";
                return "";
            }
        }

        public void LoadCustomers()
        {
            var selected = Customer == null ? null : Customer.CustomerNo;
            Customers.Clear();
            foreach (var c in _store.LoadCustomers()) Customers.Add(c);
            if (selected != null) Customer = Customers.FirstOrDefault(c => c.CustomerNo == selected);
        }

        /// <summary>Opens an order from the outbox for correction (for example after a price change).</summary>
        public void Edit(OutboxOrder order)
        {
            Clear();
            _clientOrderId = order.Request.ClientOrderId;
            _repReference = order.Request.RepReference;
            Customer = Customers.FirstOrDefault(c => c.CustomerNo == order.Request.CustomerNo);
            Delivery = order.Request.RequestedDelivery;
            Comment = order.Request.Comment;

            var pricing = _pricing();
            foreach (var l in order.Request.Lines)
            {
                Product product;
                if (!pricing.Products.TryGetValue(l.ItemNo, out product)) continue;
                var price = Customer == null ? l.UnitPrice : pricing.Calculator.UnitPrice(product, Customer.PriceGroup, Delivery ?? DateTime.Today);
                Lines.Add(new OrderLineViewModel(product, price, Recalculate) { Quantity = l.Quantity, LineDiscountPercent = l.LineDiscountPercent });
            }
            Message = order.Message;
            Recalculate();
        }

        private async void LoadStock(string itemNo)
        {
            try
            {
                var stock = (await _api.GetStockAsync(new[] { itemNo })).FirstOrDefault();
                if (SelectedProduct == null || SelectedProduct.ItemNo != itemNo || stock == null) return;
                StockText = stock.Available > 0
                    ? string.Format("In stock: {0} {1}", stock.Available, SelectedProduct.Unit)
                    : "Out of stock" + (stock.NextReceipt.HasValue ? ", expected " + stock.NextReceipt.Value.ToString("d") : "");
            }
            catch (Exception)
            {
                // Offline or no stock service: the order can be written without stock information.
            }
        }

        private void AddLine()
        {
            var existing = Lines.FirstOrDefault(l => l.Product.ItemNo == SelectedProduct.ItemNo);
            if (existing != null)
            {
                existing.Quantity += Math.Max(1, SelectedProduct.MinOrderQty);
                return;
            }

            var price = _pricing().Calculator.UnitPrice(SelectedProduct, Customer.PriceGroup, Delivery ?? DateTime.Today);
            Lines.Add(new OrderLineViewModel(SelectedProduct, price, Recalculate));
            Search = "";
            Recalculate();
        }

        private void Save()
        {
            var pricing = _pricing();
            var order = BuildOrder();
            var errors = new OrderValidator(pricing.Calendar).Validate(order, PricingService.ToCore(Customer), pricing.Products, DateTime.Now);
            if (errors.Count > 0)
            {
                Message = string.Join(Environment.NewLine, errors);
                return;
            }
            if (new DiscountPolicy().Evaluate(order, _discountLimit()) == DiscountDecision.NotAllowed)
            {
                Message = ApprovalHint;
                return;
            }

            if (_repReference == null) _repReference = RepReference.Create(_repCode(), DateTime.Today, _store.NextSequence(DateTime.Today));

            _store.SaveOutbox(new OutboxOrder
            {
                Request = new OrderSubmitRequest
                {
                    ClientOrderId = _clientOrderId,
                    RepReference = _repReference,
                    CustomerNo = Customer.CustomerNo,
                    CreatedAt = DateTime.Now,
                    RequestedDelivery = Delivery.Value,
                    PriceListVersion = pricing.Catalog.PriceListVersion,
                    Comment = Comment,
                    Lines = Lines.Select(l => new OrderLineDto
                    {
                        ItemNo = l.Product.ItemNo, Quantity = l.Quantity, UnitPrice = l.UnitPrice, LineDiscountPercent = l.LineDiscountPercent
                    }).ToList()
                },
                CustomerName = Customer.Name,
                NetTotal = order.NetTotal,
                State = OutboxState.Waiting
            });

            Clear();
            var handler = Saved;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void Clear()
        {
            Lines.Clear();
            Comment = null;
            Message = null;
            Search = "";
            _clientOrderId = Guid.NewGuid();
            _repReference = null;
            Recalculate();
        }

        private SalesOrder BuildOrder()
        {
            var order = new SalesOrder
            {
                CustomerNo = Customer == null ? null : Customer.CustomerNo,
                RequestedDelivery = Delivery ?? DateTime.Today,
                Lines = Lines.Select((l, i) => l.ToLine(i + 1)).ToList()
            };
            return order;
        }

        private void Recalculate()
        {
            OnPropertyChanged("NetTotal");
            OnPropertyChanged("VatTotal");
            OnPropertyChanged("GrossTotal");
            OnPropertyChanged("ApprovalHint");
        }
    }
}
