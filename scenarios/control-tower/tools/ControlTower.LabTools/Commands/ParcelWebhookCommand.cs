using System.Text;
using Proseware.ControlTower.Integration.Carriers;

namespace Proseware.ControlTower.LabTools.Commands;

/// <summary>Sends a signed ParcelNord tracking webhook to the carrier gateway.</summary>
public sealed class ParcelWebhookCommand : ILabCommand
{
    public string Name => "parcel-webhook";

    public string Usage => "--secret <webhook secret> [--url http://integ01.corp.local:8085/webhooks/parcelnord] " +
                           "(--file samples/parcelnord-webhook-delivered.json | --tracking <no> --reference <PF..> --event DELIVERED [--signed-by <name>])";

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var a = new LabArgs(args);
        var url = a.Get("url", "http://localhost:8085/webhooks/parcelnord");
        var secret = a.Required("secret");
        string body;
        if (a.Get("file") is { } file)
        {
            body = await File.ReadAllTextAsync(file, cancellationToken);
        }
        else
        {
            var signedBy = a.Get("signed-by");
            body = "{" +
                   $"\"trackingNumber\":\"{a.Required("tracking")}\"," +
                   $"\"reference\":\"{a.Get("reference") ?? ""}\"," +
                   "\"event\":{" +
                   $"\"type\":\"{a.Get("event", "DELIVERED")}\"," +
                   $"\"time\":\"{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\"," +
                   $"\"description\":\"Lab event\"" +
                   (signedBy is null ? "" : $",\"signedBy\":\"{signedBy}\"") +
                   "}}";
        }

        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-ParcelNord-Signature", ParcelNordClient.SignatureHeader(body, secret, DateTimeOffset.UtcNow));
        using var response = await http.SendAsync(request, cancellationToken);
        Console.WriteLine($"{(int)response.StatusCode} {response.ReasonPhrase}");
        return response.IsSuccessStatusCode ? 0 : 2;
    }
}
