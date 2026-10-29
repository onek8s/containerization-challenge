using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using log4net;
using Wingtip.FieldSales.Client.Services;
using Wingtip.FieldSales.Contracts;

namespace Wingtip.FieldSales.Client.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MainViewModel));

        private readonly ApiClient _api;
        private readonly LocalStore _store;
        private readonly SyncService _sync;
        private readonly DispatcherTimer _timer;
        private PricingService _pricing;
        private MeResponse _me;
        private string _status;
        private bool _syncing;
        private int _selectedTab;
        private string _customerFilter;
        private OutboxOrder _selectedOutbox;

        public MainViewModel(ApiClient api, LocalStore store, ClientSettings settings, MeResponse me)
        {
            _api = api;
            _store = store;
            _me = me ?? new MeResponse { UserName = api.UserName, DisplayName = api.UserName, RepCode = "", DiscountLimitPercent = 0m };
            _sync = new SyncService(api, store);

            Order = new OrderEntryViewModel(store, () => Pricing, () => _me.RepCode, () => _me.DiscountLimitPercent, api);
            Order.Saved += async (s, e) =>
            {
                RefreshOutbox();
                SelectedTab = 2;
                await SyncAsync();
            };

            Customers = new ObservableCollection<CustomerDto>();
            Outbox = new ObservableCollection<OutboxOrder>();
            Sent = new ObservableCollection<OutboxOrder>();

            SyncCommand = new AsyncCommand(SyncAsync, () => !_syncing);
            NewOrderCommand = new RelayCommand(c => { Order.Customer = (CustomerDto)c; SelectedTab = 1; }, c => c != null);
            EditOutboxCommand = new RelayCommand(_ => EditSelected(), _ => SelectedOutbox != null && SelectedOutbox.State != OutboxState.Sent);
            ResendCommand = new AsyncCommand(ResendAsync, () => SelectedOutbox != null && SelectedOutbox.State == OutboxState.NeedsReview);
            DeleteOutboxCommand = new RelayCommand(_ => DeleteSelected(), _ => SelectedOutbox != null && SelectedOutbox.State != OutboxState.Sent);

            _timer = new DispatcherTimer { Interval = settings.SyncInterval };
            _timer.Tick += async (s, e) => await SyncAsync();
            _timer.Start();

            LoadLocal();
        }

        public event EventHandler SignInRequired;
        public event EventHandler UpdateRequired;

        public OrderEntryViewModel Order { get; private set; }
        public ObservableCollection<CustomerDto> Customers { get; private set; }
        public ObservableCollection<OutboxOrder> Outbox { get; private set; }
        public ObservableCollection<OutboxOrder> Sent { get; private set; }

        public AsyncCommand SyncCommand { get; private set; }
        public RelayCommand NewOrderCommand { get; private set; }
        public RelayCommand EditOutboxCommand { get; private set; }
        public AsyncCommand ResendCommand { get; private set; }
        public RelayCommand DeleteOutboxCommand { get; private set; }

        public PricingService Pricing
        {
            get { return _pricing ?? (_pricing = new PricingService(_store.LoadCatalog())); }
        }

        public string Title
        {
            get { return "Field Sales - " + _me.DisplayName + (string.IsNullOrEmpty(_me.RepCode) ? "" : " (" + _me.RepCode + ")"); }
        }

        public string Status
        {
            get { return _status; }
            set { Set(ref _status, value); }
        }

        public string CatalogInfo
        {
            get
            {
                var c = Pricing.Catalog;
                return c.SyncedAt == DateTime.MinValue
                    ? "Price list not downloaded yet"
                    : string.Format("Price list {0}, {1} products, synchronized {2:g}", c.PriceListVersion, c.Products.Count, c.SyncedAt);
            }
        }

        public string Version
        {
            get { return DeploymentInfo.Version; }
        }

        public int SelectedTab
        {
            get { return _selectedTab; }
            set { Set(ref _selectedTab, value); }
        }

        public string CustomerFilter
        {
            get { return _customerFilter; }
            set { if (Set(ref _customerFilter, value)) FilterCustomers(); }
        }

        public OutboxOrder SelectedOutbox
        {
            get { return _selectedOutbox; }
            set { Set(ref _selectedOutbox, value); }
        }

        public int WaitingCount
        {
            get { return Outbox.Count(o => o.State == OutboxState.Waiting); }
        }

        public async Task SyncAsync()
        {
            if (_syncing) return;
            _syncing = true;
            Status = "Synchronizing...";
            try
            {
                var result = await _sync.RunAsync();
                _pricing = null;
                LoadLocal();

                if (result.UpdateRequired)
                {
                    Status = result.Error;
                    var handler = UpdateRequired;
                    if (handler != null) handler(this, EventArgs.Empty);
                }
                else if (result.Error != null)
                {
                    Status = "Offline: " + result.Error + " Orders are kept on this laptop.";
                }
                else
                {
                    Status = string.Format("Synchronized {0:t}. {1} orders sent{2}.", DateTime.Now, result.OrdersSent,
                        result.OrdersNeedingReview > 0 ? ", " + result.OrdersNeedingReview + " need your attention" : "");
                }

                try
                {
                    _me = await _api.GetMeAsync();
                    OnPropertyChanged("Title");
                }
                catch (OfflineException)
                {
                }
            }
            catch (SignInRequiredException ex)
            {
                Status = ex.Message;
                var handler = SignInRequired;
                if (handler != null) handler(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log.Error("Synchronization failed", ex);
                Status = "Synchronization failed: " + ex.Message;
            }
            finally
            {
                _syncing = false;
            }
        }

        public void Stop()
        {
            _timer.Stop();
        }

        private void LoadLocal()
        {
            FilterCustomers();
            Order.LoadCustomers();
            RefreshOutbox();
            OnPropertyChanged("CatalogInfo");
        }

        private void FilterCustomers()
        {
            Customers.Clear();
            var filter = CustomerFilter;
            foreach (var c in _store.LoadCustomers().Where(c => string.IsNullOrWhiteSpace(filter)
                || c.Name.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0
                || c.CustomerNo.StartsWith(filter, StringComparison.OrdinalIgnoreCase)
                || (c.City ?? "").StartsWith(filter, StringComparison.CurrentCultureIgnoreCase)))
                Customers.Add(c);
        }

        private void RefreshOutbox()
        {
            Outbox.Clear();
            foreach (var o in _store.LoadOutbox()) Outbox.Add(o);
            Sent.Clear();
            foreach (var o in _store.LoadSent(30)) Sent.Add(o);
            OnPropertyChanged("WaitingCount");
        }

        private void EditSelected()
        {
            Order.Edit(SelectedOutbox);
            _store.DeleteOutbox(SelectedOutbox.Request.ClientOrderId);
            RefreshOutbox();
            SelectedTab = 1;
        }

        private async Task ResendAsync()
        {
            SelectedOutbox.State = OutboxState.Waiting;
            SelectedOutbox.Message = null;
            _store.SaveOutbox(SelectedOutbox);
            await SyncAsync();
        }

        private void DeleteSelected()
        {
            _store.DeleteOutbox(SelectedOutbox.Request.ClientOrderId);
            RefreshOutbox();
        }
    }
}
