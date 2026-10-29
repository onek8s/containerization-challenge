using System.Net;
using System.Net.Http.Json;

namespace Coho.Billing.Lookup.Services;

/// <summary>Calls the billing engine control endpoint with the application pool identity.</summary>
public class EngineClient
{
    private readonly HttpClient _http;
    private readonly ILogger<EngineClient> _logger;

    public EngineClient(HttpClient http, ILogger<EngineClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<string> RequestReprintAsync(string billNumber, string requestedBy, string reason)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync("api/reprints", new { BillNumber = billNumber, RequestedBy = requestedBy, Reason = reason });
            if (response.IsSuccessStatusCode)
            {
                return null;
            }

            _logger.LogWarning("Reprint of {BillNumber} refused by the engine: {Status}", billNumber, response.StatusCode);
            return response.StatusCode switch
            {
                HttpStatusCode.NotFound => "The billing engine does not know this bill.",
                HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized => "The site is not allowed to request reprints (check ReprintCallers on the engine).",
                _ => "The billing engine answered " + (int)response.StatusCode + "."
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Billing engine not reachable");
            return "The billing engine is not reachable, try again later.";
        }
        catch (TaskCanceledException)
        {
            return "The billing engine did not answer in time.";
        }
    }
}
