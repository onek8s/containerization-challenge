using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ProductReturns.Core;
using ProductReturns.Core.Api;

namespace ProductReturns.Portal.Services
{
    public class ApiResult<T>
    {
        public bool Ok { get; set; }
        public int StatusCode { get; set; }
        public string Error { get; set; }
        public T Value { get; set; }
    }

    /// <summary>Calls the returns API. Staff actions pass the signed-in account in X-Returns-User.</summary>
    public class ReturnsApiClient
    {
        private static readonly JsonSerializerOptions Json = CreateJsonOptions();

        private readonly HttpClient _http;

        public ReturnsApiClient(HttpClient http)
        {
            _http = http;
        }

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }

        public Task<ApiResult<OrderLookupResponse>> GetOrderAsync(string orderNumber, string email, CancellationToken ct) =>
            SendAsync<OrderLookupResponse>(HttpMethod.Get, $"api/orders/{Uri.EscapeDataString(orderNumber)}?email={Uri.EscapeDataString(email)}", null, null, ct);

        public Task<ApiResult<CreateReturnResponse>> CreateReturnAsync(CreateReturnRequest request, string actor, CancellationToken ct) =>
            SendAsync<CreateReturnResponse>(HttpMethod.Post, "api/returns", request, actor, ct);

        public Task<ApiResult<ReturnDetail>> GetReturnAsync(string rma, string email, CancellationToken ct) =>
            SendAsync<ReturnDetail>(HttpMethod.Get, $"api/returns/{Uri.EscapeDataString(rma)}" + (email == null ? string.Empty : "?email=" + Uri.EscapeDataString(email)), null, null, ct);

        public Task<ApiResult<ReturnDetail>> CancelReturnAsync(string rma, string email, CancellationToken ct) =>
            SendAsync<ReturnDetail>(HttpMethod.Post, $"api/returns/{Uri.EscapeDataString(rma)}/cancel?email={Uri.EscapeDataString(email)}", null, "customer", ct);

        public Task<ApiResult<ReturnSearchResponse>> SearchAsync(ReturnStatus? status, string q, int page, CancellationToken ct) =>
            SendAsync<ReturnSearchResponse>(HttpMethod.Get,
                $"api/returns?page={page}" + (status.HasValue ? "&status=" + status.Value : string.Empty)
                + (string.IsNullOrWhiteSpace(q) ? string.Empty : "&q=" + Uri.EscapeDataString(q.Trim())), null, null, ct);

        public Task<ApiResult<Dictionary<string, int>>> SummaryAsync(CancellationToken ct) =>
            SendAsync<Dictionary<string, int>>(HttpMethod.Get, "api/returns/summary", null, null, ct);

        public Task<ApiResult<ReturnDetail>> ApproveAsync(string rma, string actor, CancellationToken ct) =>
            SendAsync<ReturnDetail>(HttpMethod.Post, $"api/returns/{Uri.EscapeDataString(rma)}/approve", null, actor, ct);

        public Task<ApiResult<ReturnDetail>> RejectAsync(string rma, string reason, string actor, CancellationToken ct) =>
            SendAsync<ReturnDetail>(HttpMethod.Post, $"api/returns/{Uri.EscapeDataString(rma)}/reject", new DecisionRequest { Reason = reason }, actor, ct);

        public Task<ApiResult<ReturnDetail>> AddNoteAsync(string rma, string text, string actor, CancellationToken ct) =>
            SendAsync<ReturnDetail>(HttpMethod.Post, $"api/returns/{Uri.EscapeDataString(rma)}/notes", new NoteRequest { Text = text }, actor, ct);

        public Task<ApiResult<ReturnDetail>> CompleteInspectionAsync(string rma, string actor, CancellationToken ct) =>
            SendAsync<ReturnDetail>(HttpMethod.Post, $"api/returns/{Uri.EscapeDataString(rma)}/complete-inspection", null, actor, ct);

        public async Task<Stream> GetLabelAsync(string rma, string email, CancellationToken ct)
        {
            var url = $"api/returns/{Uri.EscapeDataString(rma)}/label" + (email == null ? string.Empty : "?email=" + Uri.EscapeDataString(email));
            var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                response.Dispose();
                return null;
            }

            return await response.Content.ReadAsStreamAsync();
        }

        private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object body, string actor, CancellationToken ct)
        {
            using (var request = new HttpRequestMessage(method, url))
            {
                if (body != null)
                {
                    request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
                }

                if (!string.IsNullOrEmpty(actor))
                {
                    request.Headers.Add("X-Returns-User", actor);
                }

                using (var response = await _http.SendAsync(request, ct))
                {
                    var text = await response.Content.ReadAsStringAsync();
                    var result = new ApiResult<T> { StatusCode = (int)response.StatusCode, Ok = response.IsSuccessStatusCode };
                    if (response.IsSuccessStatusCode)
                    {
                        result.Value = string.IsNullOrEmpty(text) ? default : JsonSerializer.Deserialize<T>(text, Json);
                    }
                    else
                    {
                        result.Error = ProblemDetail(text) ?? (response.StatusCode == HttpStatusCode.NotFound ? "Not found." : "The returns service is not available (" + (int)response.StatusCode + ").");
                    }

                    return result;
                }
            }
        }

        private static string ProblemDetail(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text[0] != '{')
            {
                return null;
            }

            try
            {
                using (var doc = JsonDocument.Parse(text))
                {
                    if (doc.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                    {
                        return detail.GetString();
                    }

                    if (doc.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                    {
                        return message.GetString();
                    }
                }
            }
            catch (JsonException)
            {
            }

            return null;
        }
    }
}
