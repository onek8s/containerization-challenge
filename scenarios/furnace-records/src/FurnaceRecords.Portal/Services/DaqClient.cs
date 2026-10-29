using System.Net.Http.Json;

namespace FurnaceRecords.Portal.Services;

public sealed record LiveChannel(int Channel, string Name, string Role, double? Celsius, bool Open);

public sealed record FurnaceLive(
    string Code,
    string Status,
    string? Board,
    bool ControllerConnected,
    bool CycleActive,
    string? LoadNumber,
    double? SetpointCelsius,
    double? ControlCelsius,
    bool DoorOpen,
    IReadOnlyList<LiveChannel> Channels,
    long LostScans,
    DateTime UpdatedUtc,
    string? LastError);

public sealed class DaqClient
{
    private readonly HttpClient _http;
    private readonly ILogger<DaqClient> _logger;

    public DaqClient(HttpClient http, ILogger<DaqClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>Live readings, or null when the acquisition service cannot be reached.</summary>
    public async Task<IReadOnlyList<FurnaceLive>?> LiveAsync(CancellationToken ct)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<FurnaceLive>>("live", ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning("Acquisition service {Address} not reachable: {Message}", _http.BaseAddress, ex.Message);
            return null;
        }
    }
}
