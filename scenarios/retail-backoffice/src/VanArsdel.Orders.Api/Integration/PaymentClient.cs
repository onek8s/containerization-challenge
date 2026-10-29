using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using Refit;
using VanArsdel.BuildingBlocks.Exceptions;
using VanArsdel.Integration.Payments;

namespace VanArsdel.Orders.Api.Integration;

public class PspOptions
{
    public string BaseUrl { get; set; } = "https://api.klarnet.example";
    public string MerchantId { get; set; } = "";
    public string MerchantKey { get; set; } = "";
    public string WebhookSecret { get; set; } = "";

    /// <summary>Store (LocalMachine\My by thumbprint), File (PFX) or None (simulator).</summary>
    public string CertificateSource { get; set; } = "Store";
    public string CertificateThumbprint { get; set; } = "";
    public string CertificatePath { get; set; } = "";
    public string CertificatePassword { get; set; } = "";
}

public static class PspClientRegistration
{
    public static IServiceCollection AddPspClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PspOptions>(configuration.GetSection("Psp"));
        var options = configuration.GetSection("Psp").Get<PspOptions>() ?? new PspOptions();

        services.AddRefitClient<IPspApi>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(options.BaseUrl);
                c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(options.MerchantId + ":" + options.MerchantKey)));
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                var handler = new HttpClientHandler();
                var certificate = LoadCertificate(options);
                if (certificate != null)
                {
                    handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                    handler.ClientCertificates.Add(certificate);
                }
                return handler;
            })
            .AddPolicyHandler(VanArsdel.BuildingBlocks.Http.HttpClientExtensions.RetryPolicy());
        return services;
    }

    /// <summary>
    /// The PSP requires mutual TLS. Operations import the merchant certificate into the machine store
    /// of the app servers and grant the app pool identity read access to the private key.
    /// </summary>
    private static X509Certificate2 LoadCertificate(PspOptions options)
    {
        switch (options.CertificateSource)
        {
            case "Store":
                using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
                {
                    store.Open(OpenFlags.ReadOnly);
                    var found = store.Certificates.Find(X509FindType.FindByThumbprint, options.CertificateThumbprint, validOnly: false);
                    if (found.Count == 0)
                    {
                        throw new InvalidOperationException($"PSP client certificate {options.CertificateThumbprint} not found in LocalMachine\\My.");
                    }
                    return found[0];
                }
            case "File":
                return new X509Certificate2(options.CertificatePath, options.CertificatePassword, X509KeyStorageFlags.MachineKeySet);
            default:
                return null;
        }
    }
}

public class PaymentService
{
    private readonly IPspApi _psp;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(IPspApi psp, ILogger<PaymentService> logger)
    {
        _psp = psp;
        _logger = logger;
    }

    public async Task<decimal> CaptureAsync(string paymentReference, decimal amount, string orderNo)
    {
        try
        {
            var result = await _psp.CaptureAsync(paymentReference, new PspCaptureRequest { Amount = Money.ToMinor(amount), Reference = orderNo, Final = true }, "capture-" + orderNo);
            _logger.LogInformation("Captured {Amount} for {OrderNo}: {Status}", amount, orderNo, result.Status);
            return Money.FromMinor(result.Amount);
        }
        catch (ApiException ex)
        {
            throw new UpstreamException("PSP", $"Capture of {orderNo} failed with {(int)ex.StatusCode}: {ex.Content}", ex);
        }
    }

    public async Task CancelAsync(string paymentReference, string orderNo)
    {
        try
        {
            await _psp.CancelAsync(paymentReference, "cancel-" + orderNo);
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogWarning("Payment {Reference} of {OrderNo} could not be cancelled (already captured?)", paymentReference, orderNo);
        }
    }

    public async Task<decimal> RefundAsync(string paymentReference, decimal amount, string reference, string reason)
    {
        var result = await _psp.RefundAsync(paymentReference, new PspRefundRequest { Amount = Money.ToMinor(amount), Reference = reference, Reason = reason }, "refund-" + reference);
        return Money.FromMinor(result.Amount);
    }
}
