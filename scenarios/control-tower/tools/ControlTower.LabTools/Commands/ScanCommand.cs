using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Proseware.ControlTower.LabTools.Commands;

/// <summary>
/// Simulates a terminal handheld: scans the packages expected at a terminal today (or the given SSCCs)
/// and posts them to the scanning API in batches, like the TC51/TC52 app does after it gets Wi-Fi back.
/// </summary>
public sealed class ScanCommand : ILabCommand
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public string Name => "scan";

    public string Usage =>
        "scan --api http://scan.corp.local --terminal FIVAN [--kind Receive|Load|Unload|Deliver|Damage] " +
        "[--device TC51-VAN-01 --device-key <key> | --token-url https://login.corp.local/connect/token --client-secret <secret>] " +
        "[--sscc <sscc>[,<sscc>...]] [--batch 25] [--miss 5] [--repeat 1]";

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        string? Arg(string name, string? fallback = null)
        {
            var i = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        var api = Arg("--api");
        var terminal = Arg("--terminal");
        if (api is null || terminal is null)
        {
            Console.Error.WriteLine("Usage: " + Usage);
            return 2;
        }
        var device = Arg("--device", "TC52-LAB-01")!;
        var kind = Arg("--kind", "Receive")!;
        var batchSize = int.Parse(Arg("--batch", "25")!);
        var missPercent = int.Parse(Arg("--miss", "5")!);
        var repeat = int.Parse(Arg("--repeat", "1")!);

        using var http = new HttpClient { BaseAddress = new Uri(api.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(30) };
        if (Arg("--device-key") is { } key)
        {
            http.DefaultRequestHeaders.Add("X-Device-Key", $"{device}:{key}");
        }
        else if (Arg("--token-url") is { } tokenUrl)
        {
            var token = await GetTokenAsync(tokenUrl, Arg("--client-id", "scanner")!, Arg("--client-secret") ?? "", cancellationToken);
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        List<string> ssccs;
        if (Arg("--sscc") is { } list)
        {
            ssccs = list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }
        else
        {
            var expected = await http.GetFromJsonAsync<List<ExpectedPackage>>($"api/terminals/{Uri.EscapeDataString(terminal)}/expected", Json, cancellationToken)
                           ?? new List<ExpectedPackage>();
            ssccs = expected.Where(e => !e.Received).Select(e => e.Sscc).ToList();
            Console.WriteLine($"{expected.Count} packages expected at {terminal} today, {ssccs.Count} not received yet.");
        }

        var random = new Random();
        var scans = new List<object>();
        var now = DateTimeOffset.UtcNow;
        foreach (var sscc in ssccs)
        {
            // Some labels are damaged or the package is left behind.
            if (random.Next(100) < missPercent) continue;
            for (var r = 0; r < repeat; r++)
            {
                scans.Add(new
                {
                    clientScanId = Guid.NewGuid(),
                    barcode = random.Next(3) == 0 ? "(00)" + sscc : "00" + sscc,
                    kind,
                    terminal,
                    scannedAt = now.AddSeconds(scans.Count * random.Next(4, 25)),
                    doorOrBay = $"D{random.Next(1, 24):00}",
                    userName = "lab.scanner",
                });
            }
        }

        var accepted = 0;
        var other = 0;
        foreach (var chunk in scans.Chunk(batchSize))
        {
            var response = await http.PostAsJsonAsync("api/scans", new { deviceId = device, scans = chunk }, Json, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"POST api/scans: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(cancellationToken)}");
                return 1;
            }
            var results = await response.Content.ReadFromJsonAsync<List<ScanResult>>(Json, cancellationToken) ?? new();
            accepted += results.Count(r => r.Outcome == "Accepted");
            other += results.Count(r => r.Outcome != "Accepted");
            foreach (var r in results.Where(r => r.Outcome != "Accepted"))
                Console.WriteLine($"  {r.ClientScanId}: {r.Outcome} {r.Message}");
        }
        Console.WriteLine($"{scans.Count} scans sent from {device}: {accepted} accepted, {other} other.");
        return 0;
    }

    private static async Task<string> GetTokenAsync(string tokenUrl, string clientId, string secret, CancellationToken ct)
    {
        using var http = new HttpClient();
        var response = await http.PostAsync(tokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = secret,
            ["scope"] = "scan.api"
        }), ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    private sealed record ExpectedPackage(string Sscc, string ShipmentNumber, bool Received);

    private sealed record ScanResult(Guid ClientScanId, string Outcome, long? ScanId, string? Message);
}
