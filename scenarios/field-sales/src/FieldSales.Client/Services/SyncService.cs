using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using log4net;
using Wingtip.FieldSales.Contracts;

namespace Wingtip.FieldSales.Client.Services
{
    public class SyncResult
    {
        public int ProductsChanged { get; set; }
        public int CustomersReceived { get; set; }
        public int OrdersSent { get; set; }
        public int OrdersNeedingReview { get; set; }
        public bool UpdateRequired { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Downloads catalog and customer changes and sends the outbox. Orders that fail because of the network stay in
    /// the outbox and are sent on the next run; the server recognises a resent order by its ClientOrderId.
    /// </summary>
    public class SyncService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SyncService));
        private readonly ApiClient _api;
        private readonly LocalStore _store;

        public SyncService(ApiClient api, LocalStore store)
        {
            _api = api;
            _store = store;
        }

        public async Task<SyncResult> RunAsync()
        {
            var result = new SyncResult();
            try
            {
                result.ProductsChanged = await SyncCatalogAsync();
                var customers = await _api.GetCustomersAsync();
                _store.SaveCustomers(customers);
                result.CustomersReceived = customers.Count;

                await SendOutboxAsync(result);
                await RefreshStatusesAsync();
            }
            catch (ApiException ex) when ((int)ex.Status == 426)
            {
                result.UpdateRequired = true;
                result.Error = ex.Message;
            }
            catch (OfflineException ex)
            {
                result.Error = ex.Message;
            }

            Log.InfoFormat("Sync: {0} products, {1} customers, {2} orders sent, {3} need review{4}",
                result.ProductsChanged, result.CustomersReceived, result.OrdersSent, result.OrdersNeedingReview,
                result.Error == null ? "" : " (" + result.Error + ")");
            return result;
        }

        private async Task<int> SyncCatalogAsync()
        {
            var cache = _store.LoadCatalog();
            var changes = await _api.GetCatalogAsync(cache.Version);

            if (changes.FullRefresh) cache.Products.Clear();
            foreach (var p in changes.Products) cache.Products[p.ItemNo] = p;

            cache.Version = changes.Version;
            cache.PriceListVersion = changes.PriceListVersion;
            cache.PriceRules = changes.PriceRules;
            cache.Promotions = changes.Promotions;
            cache.Holidays = changes.Holidays;
            cache.CutOff = changes.CutOff;
            cache.SyncedAt = DateTime.Now;
            _store.SaveCatalog(cache);
            return changes.Products.Count;
        }

        private async Task SendOutboxAsync(SyncResult result)
        {
            foreach (var order in _store.LoadOutbox().Where(o => o.State == OutboxState.Waiting))
            {
                order.Attempts++;
                SubmitOutcome outcome;
                try
                {
                    outcome = await _api.SubmitOrderAsync(order.Request);
                }
                catch (OfflineException)
                {
                    _store.SaveOutbox(order);
                    throw;
                }

                var response = outcome.Response;
                switch (outcome.Status)
                {
                    case HttpStatusCode.Created:
                    case HttpStatusCode.OK:
                        order.State = OutboxState.Sent;
                        order.OrderNo = response.OrderNo;
                        order.ServerStatus = response.Status;
                        order.SentAt = DateTime.Now;
                        order.Message = response.Status == "PendingApproval" ? "Waiting for sales manager approval." : null;
                        result.OrdersSent++;
                        break;
                    case HttpStatusCode.Conflict:
                        order.State = OutboxState.NeedsReview;
                        order.Message = "Prices have changed: " + string.Join(", ", response.PriceDifferences.Select(d =>
                            string.Format("{0} {1:N2} -> {2:N2}", d.ItemNo, d.OrderedPrice, d.CurrentPrice))) + ". Check with the customer.";
                        result.OrdersNeedingReview++;
                        break;
                    default:
                        order.State = OutboxState.NeedsReview;
                        order.Message = string.Join(" ", response.Errors ?? new List<string>());
                        result.OrdersNeedingReview++;
                        break;
                }
                _store.SaveOutbox(order);
            }
        }

        private async Task RefreshStatusesAsync()
        {
            var sent = _store.LoadSent(30).ToDictionary(o => o.Request.ClientOrderId);
            if (sent.Count == 0) return;

            foreach (var summary in await _api.GetRecentOrdersAsync(30))
            {
                OutboxOrder local;
                if (!sent.TryGetValue(summary.ClientOrderId, out local) || local.ServerStatus == summary.Status) continue;

                local.ServerStatus = summary.Status;
                local.Message = summary.Status == "Rejected" ? "Rejected: " + summary.DecisionComment : summary.DecisionComment;
                _store.SaveOutbox(local);
            }
        }
    }
}
