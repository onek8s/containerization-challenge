using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace VanArsdel.BackOffice.Services;

public class ApiException : Exception
{
    public ApiException(HttpStatusCode status, string message) : base(message)
    {
        Status = status;
    }

    public HttpStatusCode Status { get; }
}

/// <summary>
/// All calls go through the API gateway with the signed-in user's token, so the services
/// apply the user's roles (a merchandiser cannot refund, customer service cannot publish).
/// </summary>
public class RetailApi
{
    private readonly HttpClient _http;
    private readonly TokenProvider _tokens;

    public RetailApi(HttpClient http, TokenProvider tokens)
    {
        _http = http;
        _tokens = tokens;
    }

    public Task<Paged<ProductRow>> ProductsAsync(string search, string status, int page) =>
        GetAsync<Paged<ProductRow>>($"catalog/products?search={Esc(search)}&status={Esc(status)}&page={page}&pageSize=50");

    public Task PublishProductAsync(string styleCode) => PostAsync($"catalog/products/{Esc(styleCode)}/publish", null);

    public Task<Paged<OrderRow>> OrdersAsync(string status, string email, int page) =>
        GetAsync<Paged<OrderRow>>($"orders/orders?status={Esc(status)}&email={Esc(email)}&page={page}&pageSize=50");

    public Task<OrderView> OrderAsync(string orderNo) => GetAsync<OrderView>($"orders/orders/{Esc(orderNo)}");

    public Task CancelOrderAsync(string orderNo, string reason) => PostAsync($"orders/orders/{Esc(orderNo)}/cancel", new { reason });

    public Task<List<CustomerRow>> CustomersAsync(string q) => GetAsync<List<CustomerRow>>($"customers/customers?q={Esc(q)}&take=50");

    public Task EraseCustomerAsync(string id) => PostAsync($"customers/customers/{Esc(id)}/erase", null);

    public Task<List<ReturnRow>> ReturnsAsync(string status) => GetAsync<List<ReturnRow>>($"returns/returns?status={Esc(status)}&take=200");

    /// <summary>Grades every line the same; per-line grading is done in the returns centre scanner app.</summary>
    public async Task InspectReturnAsync(string rmaNo, string grade)
    {
        var detail = await GetAsync<ReturnDetail>($"returns/returns/{Esc(rmaNo)}");
        var grades = detail.Lines.Select(l => l.Sku).Distinct().ToDictionary(sku => sku, _ => grade);
        await PostAsync($"returns/returns/{Esc(rmaNo)}/inspect", new { grades });
    }

    public Task<List<StoreRow>> StoresAsync() => GetAsync<List<StoreRow>>("storeops/stores?includeClosed=true");

    public Task<Dictionary<string, object>> StoreDashboardAsync(string code) => GetAsync<Dictionary<string, object>>($"storeops/stores/{Esc(code)}/dashboard");

    public Task<List<DayTotal>> DailySalesAsync(int days) => GetAsync<List<DayTotal>>($"reporting/sales/daily?days={days}");

    public Task<List<ReportInfo>> ReportsAsync() => GetAsync<List<ReportInfo>>("reporting/reports");

    public Task<List<ReportRun>> ReportRunsAsync() => GetAsync<List<ReportRun>>("reporting/reports/runs?take=30");

    public async Task<ReportReadyResult> RunReportAsync(string name, DateTime date)
    {
        using var response = await SendAsync(HttpMethod.Post, $"reporting/reports/{Esc(name)}/run?date={date:yyyy-MM-dd}", null);
        return await response.Content.ReadFromJsonAsync<ReportReadyResult>();
    }

    private async Task<T> GetAsync<T>(string path)
    {
        using var response = await SendAsync(HttpMethod.Get, path, null);
        return await response.Content.ReadFromJsonAsync<T>();
    }

    private async Task PostAsync(string path, object body)
    {
        using var response = await SendAsync(HttpMethod.Post, path, body);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object body)
    {
        if (_tokens.IsExpired)
        {
            throw new ApiException(HttpStatusCode.Unauthorized, "Your session has expired. Reload the page to sign in again.");
        }
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens.AccessToken);
        if (body != null) request.Content = JsonContent.Create(body);
        else if (method == HttpMethod.Post) request.Content = new StringContent("");

        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync();
            response.Dispose();
            throw new ApiException(response.StatusCode, response.StatusCode switch
            {
                HttpStatusCode.Forbidden => "You do not have the role for this.",
                HttpStatusCode.NotFound => "Not found.",
                _ => $"{(int)response.StatusCode}: {(text.Length > 300 ? text.Substring(0, 300) : text)}",
            });
        }
        return response;
    }

    private static string Esc(string value) => Uri.EscapeDataString(value ?? "");
}
