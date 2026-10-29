using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using RestSharp;

namespace CityPermits.Integration;

public sealed class PaystreamOptions
{
    public string BaseUrl { get; set; } = "https://api.paystream.co.uk/v2/";
    public string MerchantId { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public string ReturnUrl { get; set; } = "https://permits.ashford.gov.uk/payments/return";
    /// <summary>Live or Simulator (no calls, the portal shows a local payment page).</summary>
    public string Mode { get; set; } = "Live";
}

public sealed record PaymentSession(string SessionId, string RedirectUrl, string PaymentId);

/// <summary>Paystream hosted payment page (card payments for fees).</summary>
public sealed class PaystreamClient
{
    private readonly PaystreamOptions _options;

    public PaystreamClient(PaystreamOptions options) => _options = options;

    public PaymentSession CreateSession(string merchantReference, decimal amount, string description, string email)
    {
        if (_options.Mode == "Simulator")
        {
            var id = "SIM-" + Guid.NewGuid().ToString("N")[..12];
            return new PaymentSession(id, "/payments/simulator?session=" + id, id);
        }
        var client = new RestClient(_options.BaseUrl);
        client.AddDefaultHeader("Authorization", "Bearer " + _options.ApiKey);
        var req = new RestRequest("sessions", Method.POST);
        req.AddJsonBody(new
        {
            merchant_id = _options.MerchantId,
            merchant_reference = merchantReference,
            amount_minor = (long)Math.Round(amount * 100),
            currency = "GBP",
            description,
            customer_email = email,
            return_url = _options.ReturnUrl
        });
        var res = client.Execute(req);
        if (!res.IsSuccessful) throw new InvalidOperationException($"Paystream session failed: {(int)res.StatusCode} {res.Content}");
        dynamic body = JsonConvert.DeserializeObject(res.Content)!;
        return new PaymentSession((string)body.session_id, (string)body.redirect_url, (string)body.payment_id);
    }

    /// <summary>Paystream-Signature: t=timestamp,v1=hex(HMAC-SHA256(secret, timestamp + "." + body)).</summary>
    public bool VerifySignature(string header, string body, DateTimeOffset now, TimeSpan? tolerance = null)
    {
        var parts = header.Split(',').Select(p => p.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0].Trim(), p => p[1].Trim());
        if (!parts.TryGetValue("t", out var ts) || !parts.TryGetValue("v1", out var sig) || !long.TryParse(ts, out var t)) return false;
        if (Math.Abs(now.ToUnixTimeSeconds() - t) > (tolerance ?? TimeSpan.FromMinutes(5)).TotalSeconds) return false;
        var expected = Sign(_options.WebhookSecret, ts, body);
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(sig.ToLowerInvariant()));
    }

    public static string Sign(string secret, string timestamp, string body)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(timestamp + "." + body))).ToLowerInvariant();
    }

    public static IReadOnlyList<SettlementLine> ParseSettlement(TextReader reader)
    {
        var lines = new List<SettlementLine>();
        string? line;
        reader.ReadLine();
        while ((line = reader.ReadLine()) is not null)
        {
            var f = line.Split(',');
            if (f.Length < 6) continue;
            lines.Add(new SettlementLine(f[0], f[1], decimal.Parse(f[2], System.Globalization.CultureInfo.InvariantCulture) / 100m,
                decimal.Parse(f[3], System.Globalization.CultureInfo.InvariantCulture) / 100m, DateTime.Parse(f[4], System.Globalization.CultureInfo.InvariantCulture), f[5]));
        }
        return lines;
    }
}

public sealed record SettlementLine(string PaymentId, string MerchantReference, decimal Gross, decimal Fee, DateTime SettledUtc, string Batch);
