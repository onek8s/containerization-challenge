using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Wingtip.FieldSales.Contracts;

namespace Wingtip.FieldSales.Client.Services
{
    public class ApiException : Exception
    {
        public ApiException(HttpStatusCode status, string message) : base(message)
        {
            Status = status;
        }

        public HttpStatusCode Status { get; private set; }
    }

    public class SignInRequiredException : Exception
    {
        public SignInRequiredException(string message) : base(message) { }
    }

    public class OfflineException : Exception
    {
        public OfflineException(string message, Exception inner) : base(message, inner) { }
    }

    public class SubmitOutcome
    {
        public HttpStatusCode Status { get; set; }
        public OrderSubmitResponse Response { get; set; }
    }

    /// <summary>HTTP client for the Field Sales API. Renews the access token with the refresh token when it expires.</summary>
    public class ApiClient : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ApiClient));
        private readonly ClientSettings _settings;
        private readonly TokenStore _tokens;
        private readonly HttpClient _http;
        private StoredToken _token;

        public ApiClient(ClientSettings settings, TokenStore tokens)
        {
            _settings = settings;
            _tokens = tokens;
            _token = tokens.Load();

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            _http = new HttpClient(new HttpClientHandler { UseDefaultCredentials = false, UseProxy = true })
            {
                BaseAddress = settings.ApiBaseUrl,
                Timeout = settings.RequestTimeout
            };
            _http.DefaultRequestHeaders.Add("X-FieldSales-Client", DeploymentInfo.Version);
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("FieldSales/" + DeploymentInfo.Version);
        }

        public string UserName
        {
            get { return _token == null ? null : _token.UserName; }
        }

        public bool HasSession
        {
            get { return _token != null && !string.IsNullOrEmpty(_token.RefreshToken); }
        }

        public async Task SignInAsync(string userName, string password)
        {
            var form = new Dictionary<string, string>
            {
                { "grant_type", "password" },
                { "username", userName },
                { "password", password },
                { "client_id", _settings.ClientId }
            };
            await RequestTokenAsync(form, userName);
        }

        public void SignOut()
        {
            _token = null;
            _tokens.Clear();
        }

        public Task<MeResponse> GetMeAsync()
        {
            return GetAsync<MeResponse>("api/me");
        }

        public Task<CatalogResponse> GetCatalogAsync(long since)
        {
            return GetAsync<CatalogResponse>("api/catalog?since=" + since);
        }

        public Task<List<CustomerDto>> GetCustomersAsync()
        {
            return GetAsync<List<CustomerDto>>("api/customers");
        }

        public Task<List<OrderSummaryDto>> GetRecentOrdersAsync(int days)
        {
            return GetAsync<List<OrderSummaryDto>>("api/orders?days=" + days);
        }

        public Task<List<StockDto>> GetStockAsync(IEnumerable<string> itemNos)
        {
            return GetAsync<List<StockDto>>("api/stock?items=" + Uri.EscapeDataString(string.Join(",", itemNos)));
        }

        public async Task<SubmitOutcome> SubmitOrderAsync(OrderSubmitRequest request)
        {
            var body = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            using (var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, "api/orders") { Content = body }))
            {
                var text = await response.Content.ReadAsStringAsync();
                var code = (int)response.StatusCode;
                if (code == 201 || code == 200 || code == 409 || code == 422)
                {
                    return new SubmitOutcome { Status = response.StatusCode, Response = JsonConvert.DeserializeObject<OrderSubmitResponse>(text) };
                }
                throw new ApiException(response.StatusCode, ErrorMessage(text, response.ReasonPhrase));
            }
        }

        public async Task<bool> IsOfficeReachableAsync()
        {
            try
            {
                using (var response = await _http.GetAsync("api/status"))
                {
                    return response.IsSuccessStatusCode;
                }
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
        }

        private async Task<T> GetAsync<T>(string path)
        {
            using (var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, path)))
            {
                var text = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode) throw new ApiException(response.StatusCode, ErrorMessage(text, response.ReasonPhrase));
                return JsonConvert.DeserializeObject<T>(text);
            }
        }

        private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> create)
        {
            if (_token == null) throw new SignInRequiredException("Sign in to synchronize.");
            if (_token.AccessTokenExpiresUtc <= DateTime.UtcNow.AddMinutes(1)) await RefreshAsync();

            try
            {
                var request = create();
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token.AccessToken);
                var response = await _http.SendAsync(request);
                if (response.StatusCode != HttpStatusCode.Unauthorized) return response;

                // Token was revoked or the server keys changed: try once with a new access token.
                response.Dispose();
                await RefreshAsync();
                request = create();
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token.AccessToken);
                return await _http.SendAsync(request);
            }
            catch (HttpRequestException ex)
            {
                throw new OfflineException("The sales office cannot be reached.", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new OfflineException("The sales office did not answer in time.", ex);
            }
        }

        private async Task RefreshAsync()
        {
            if (_token == null || string.IsNullOrEmpty(_token.RefreshToken)) throw new SignInRequiredException("Sign in again.");
            var form = new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", _token.RefreshToken },
                { "client_id", _settings.ClientId }
            };
            await RequestTokenAsync(form, _token.UserName);
        }

        private async Task RequestTokenAsync(Dictionary<string, string> form, string userName)
        {
            HttpResponseMessage response;
            try
            {
                response = await _http.PostAsync("token", new FormUrlEncodedContent(form));
            }
            catch (HttpRequestException ex)
            {
                throw new OfflineException("The sales office cannot be reached.", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new OfflineException("The sales office did not answer in time.", ex);
            }

            using (response)
            {
                var text = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    var error = JObject.Parse(text);
                    Log.WarnFormat("Token request for {0} refused: {1}", userName, error.Value<string>("error"));
                    if (form["grant_type"] == "refresh_token") SignOut();
                    throw new SignInRequiredException(error.Value<string>("error_description") ?? "Sign in again.");
                }
                if (!response.IsSuccessStatusCode) throw new ApiException(response.StatusCode, ErrorMessage(text, response.ReasonPhrase));

                var json = JObject.Parse(text);
                _token = new StoredToken
                {
                    UserName = json.Value<string>("userName") ?? userName,
                    AccessToken = json.Value<string>("access_token"),
                    AccessTokenExpiresUtc = DateTime.UtcNow.AddSeconds(json.Value<int>("expires_in")),
                    RefreshToken = json.Value<string>("refresh_token")
                };
                _tokens.Save(_token);
            }
        }

        private static string ErrorMessage(string body, string fallback)
        {
            try
            {
                var json = JObject.Parse(body);
                return json.Value<string>("Message") ?? json.Value<string>("message") ?? fallback;
            }
            catch (JsonException)
            {
                return fallback;
            }
        }

        public void Dispose()
        {
            _http.Dispose();
        }
    }
}
