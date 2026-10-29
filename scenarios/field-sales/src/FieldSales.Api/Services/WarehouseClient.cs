using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Newtonsoft.Json;
using Polly;
using Polly.CircuitBreaker;
using Wingtip.FieldSales.Contracts;

namespace Wingtip.FieldSales.Api.Services
{
    /// <summary>
    /// Stock levels from the warehouse management system's REST interface. Answers are cached for a few minutes
    /// because every rep's product search asks for them; when the WMS is down the circuit opens and the client
    /// simply shows no stock information.
    /// </summary>
    public class WarehouseClient
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(WarehouseClient));
        private static readonly MemoryCache Cache = new MemoryCache("FieldSales.Stock");
        private static readonly HttpClient Http = CreateClient();

        private static readonly AsyncCircuitBreakerPolicy Breaker = Policy
            .Handle<HttpRequestException>().Or<TaskCanceledException>()
            .CircuitBreakerAsync(3, TimeSpan.FromMinutes(1),
                (ex, wait) => Log.WarnFormat("Warehouse stock service unavailable, pausing calls for {0}: {1}", wait, ex.Message),
                () => Log.Info("Warehouse stock service available again"));

        private static readonly IAsyncPolicy Retry = Policy
            .Handle<HttpRequestException>().Or<TaskCanceledException>()
            .WaitAndRetryAsync(new[] { TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(800) });

        private readonly TimeSpan _cacheTime = TimeSpan.FromMinutes(
            double.Parse(ConfigurationManager.AppSettings["FieldSales:StockCacheMinutes"] ?? "5", System.Globalization.CultureInfo.InvariantCulture));

        public async Task<List<StockDto>> GetStockAsync(IEnumerable<string> itemNos)
        {
            var wanted = itemNos.Select(i => i.Trim().ToUpperInvariant()).Where(i => i.Length > 0).Distinct().Take(100).ToList();
            var result = new List<StockDto>();
            var missing = new List<string>();

            foreach (var item in wanted)
            {
                var cached = Cache.Get("stock:" + item) as StockDto;
                if (cached != null) result.Add(cached);
                else missing.Add(item);
            }

            if (missing.Count == 0 || Http.BaseAddress == null) return result;

            try
            {
                var fetched = await Breaker.ExecuteAsync(() => Retry.ExecuteAsync(() => FetchAsync(missing)));
                foreach (var s in fetched)
                {
                    Cache.Set("stock:" + s.ItemNo, s, DateTimeOffset.Now.Add(_cacheTime));
                    result.Add(s);
                }
            }
            catch (BrokenCircuitException)
            {
                // Already logged when the circuit opened.
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                Log.Warn("Stock query failed for " + missing.Count + " items", ex);
            }

            return result;
        }

        private static async Task<List<StockDto>> FetchAsync(List<string> items)
        {
            using (var response = await Http.GetAsync("stock?site=DC1&items=" + Uri.EscapeDataString(string.Join(",", items))))
            {
                if (response.StatusCode == HttpStatusCode.ServiceUnavailable || (int)response.StatusCode >= 500)
                    throw new HttpRequestException("Warehouse service returned " + (int)response.StatusCode);
                response.EnsureSuccessStatusCode();

                var rows = JsonConvert.DeserializeObject<List<WmsStockRow>>(await response.Content.ReadAsStringAsync());
                return rows.Select(r => new StockDto
                {
                    ItemNo = r.Sku,
                    Available = Math.Max(0, r.OnHand - r.Reserved),
                    NextReceipt = r.NextInbound
                }).ToList();
            }
        }

        private static HttpClient CreateClient()
        {
            var settings = ConfigurationManager.AppSettings;
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var url = settings["FieldSales:WarehouseApiUrl"];
            if (!string.IsNullOrEmpty(url)) client.BaseAddress = new Uri(url.EndsWith("/") ? url : url + "/");

            var user = settings["FieldSales:WarehouseApiUser"];
            if (!string.IsNullOrEmpty(user))
            {
                var raw = Encoding.ASCII.GetBytes(user + ":" + settings["FieldSales:WarehouseApiPassword"]);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(raw));
            }
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        private class WmsStockRow
        {
            public string Sku { get; set; }
            public int OnHand { get; set; }
            public int Reserved { get; set; }
            public DateTime? NextInbound { get; set; }
        }
    }
}
