using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Litware.OrderHub.Integration.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Litware.OrderHub.Integration.Webshop
{
    /// <summary>WooCommerce REST API v3 of the webshop (REST API key with read/write permission).</summary>
    public class WebshopApiClient : IDisposable
    {
        private readonly HttpClient _client;

        public WebshopApiClient(Uri baseAddress, string consumerKey, string consumerSecret, TimeSpan timeout)
        {
            _client = new HttpClient { BaseAddress = baseAddress, Timeout = timeout };
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes(consumerKey + ":" + consumerSecret)));
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("LitwareOrderHub/3.2");
        }

        public static WebshopApiClient Create(SettingsReader settings)
        {
            string url = settings.Get("Webshop.ApiUrl", "https://shop.litware-outdoor.example/wp-json/wc/v3/");
            if (!url.EndsWith("/")) url += "/";
            return new WebshopApiClient(new Uri(url), settings.Require("Webshop.ConsumerKey"), settings.Require("Webshop.ConsumerSecret"),
                settings.GetTime("Webshop.Timeout", TimeSpan.FromSeconds(30)));
        }

        public Task CompleteOrderAsync(string orderId, string carrier, string trackingNumber, string trackingUrl, CancellationToken ct)
        {
            var body = new JObject
            {
                ["status"] = "completed",
                ["meta_data"] = new JArray
                {
                    new JObject { ["key"] = "_litware_carrier", ["value"] = carrier },
                    new JObject { ["key"] = "_litware_tracking_number", ["value"] = trackingNumber },
                    new JObject { ["key"] = "_litware_tracking_url", ["value"] = trackingUrl ?? "" }
                }
            };
            return SendAsync(HttpMethod.Put, "orders/" + orderId, body, ct);
        }

        public async Task HoldOrderAsync(string orderId, string reason, CancellationToken ct)
        {
            await SendAsync(HttpMethod.Put, "orders/" + orderId, new JObject { ["status"] = "on-hold" }, ct);
            await AddNoteAsync(orderId, "Order hub: " + reason, ct);
        }

        public Task AddNoteAsync(string orderId, string note, CancellationToken ct)
        {
            return SendAsync(HttpMethod.Post, "orders/" + orderId + "/notes", new JObject { ["note"] = note, ["customer_note"] = false }, ct);
        }

        /// <summary>Looks up webshop product ids by SKU (simple products and variations that have their own SKU).</summary>
        public async Task<IDictionary<string, WebshopProductRef>> FindProductsAsync(IEnumerable<string> skus, CancellationToken ct)
        {
            var result = new Dictionary<string, WebshopProductRef>(StringComparer.OrdinalIgnoreCase);
            foreach (var chunk in skus.Distinct().Select((s, i) => new { s, i }).GroupBy(x => x.i / 50, x => x.s))
            {
                string query = "products?per_page=100&sku=" + Uri.EscapeDataString(string.Join(",", chunk));
                var products = await GetAsync<List<ProductDto>>(query, ct);
                foreach (var p in products.Where(p => !string.IsNullOrEmpty(p.Sku)))
                {
                    result[p.Sku] = new WebshopProductRef { Id = p.Id, ParentId = p.ParentId, ManageStock = p.ManageStock };
                }
            }
            return result;
        }

        /// <summary>Updates stock quantities; the batch endpoint accepts at most 100 updates per call.</summary>
        public async Task<int> UpdateStockAsync(IEnumerable<KeyValuePair<WebshopProductRef, int>> levels, CancellationToken ct)
        {
            int updated = 0;
            var all = levels.Where(l => l.Key.ManageStock).ToList();

            foreach (var group in all.Where(l => l.Key.ParentId == 0).Select((l, i) => new { l, i }).GroupBy(x => x.i / 100, x => x.l))
            {
                var body = new JObject { ["update"] = new JArray(group.Select(l => new JObject { ["id"] = l.Key.Id, ["stock_quantity"] = l.Value })) };
                await SendAsync(HttpMethod.Post, "products/batch", body, ct);
                updated += group.Count();
            }

            foreach (var parent in all.Where(l => l.Key.ParentId != 0).GroupBy(l => l.Key.ParentId))
            {
                var body = new JObject { ["update"] = new JArray(parent.Select(l => new JObject { ["id"] = l.Key.Id, ["stock_quantity"] = l.Value })) };
                await SendAsync(HttpMethod.Post, "products/" + parent.Key.ToString(CultureInfo.InvariantCulture) + "/variations/batch", body, ct);
                updated += parent.Count();
            }
            return updated;
        }

        private async Task<T> GetAsync<T>(string path, CancellationToken ct)
        {
            using (var response = await Resilience.Http("Webshop GET " + path).ExecuteAsync(c => _client.GetAsync(path, c), ct))
            {
                await EnsureSuccessAsync(response, "GET " + path);
                return JsonConvert.DeserializeObject<T>(await response.Content.ReadAsStringAsync());
            }
        }

        private async Task SendAsync(HttpMethod method, string path, JObject body, CancellationToken ct)
        {
            string json = body.ToString(Formatting.None);
            using (var response = await Resilience.Http("Webshop " + method + " " + path).ExecuteAsync(c =>
                       _client.SendAsync(new HttpRequestMessage(method, path) { Content = new StringContent(json, Encoding.UTF8, "application/json") }, c), ct))
            {
                await EnsureSuccessAsync(response, method + " " + path);
            }
        }

        private static async Task EnsureSuccessAsync(HttpResponseMessage response, string what)
        {
            if (response.IsSuccessStatusCode) return;
            string text = response.Content == null ? "" : await response.Content.ReadAsStringAsync();
            if (text.Length > 500) text = text.Substring(0, 500);
            throw new HttpRequestException("Webshop " + what + " returned " + (int)response.StatusCode + ": " + text);
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        private class ProductDto
        {
            [JsonProperty("id")] public long Id { get; set; }
            [JsonProperty("parent_id")] public long ParentId { get; set; }
            [JsonProperty("sku")] public string Sku { get; set; }
            [JsonProperty("manage_stock")] public bool ManageStock { get; set; }
        }
    }

    public class WebshopProductRef
    {
        public long Id { get; set; }
        public long ParentId { get; set; }
        public bool ManageStock { get; set; }
    }
}
