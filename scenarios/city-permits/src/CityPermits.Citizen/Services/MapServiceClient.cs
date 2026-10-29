using System.Net.Http.Json;
using CityPermits.Contracts;
using CityPermits.Domain;

namespace CityPermits.Citizen.Services;

/// <summary>Client for the city map service (site checks, parcel lookups, map exports).</summary>
public sealed class MapServiceClient
{
    private readonly HttpClient _http;

    public MapServiceClient(HttpClient http) => _http = http;

    public async Task<SiteConstraints?> CheckSiteAsync(string parcelId, string? footprintWkt = null)
    {
        var res = await _http.PostAsJsonAsync("site-checks", new SiteCheckRequest(parcelId, footprintWkt));
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<SiteConstraints>() : null;
    }

    public Task<ParcelDto?> GetParcelAsync(string parcelId) => _http.GetFromJsonAsync<ParcelDto>($"parcels/{Uri.EscapeDataString(parcelId)}");

    public async Task<byte[]?> ExportAsync(MapExportRequest req)
    {
        var res = await _http.PostAsJsonAsync("exports", req);
        return res.IsSuccessStatusCode ? await res.Content.ReadAsByteArrayAsync() : null;
    }
}
