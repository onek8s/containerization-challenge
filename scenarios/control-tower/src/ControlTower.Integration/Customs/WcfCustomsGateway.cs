using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proseware.ControlTower.Domain.Customs;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Integration.Customs;

/// <summary>Customs web service over HTTPS with the client certificate from the machine certificate store.</summary>
public sealed class WcfCustomsGateway : ICustomsGateway, IDisposable
{
    private readonly CustomsOptions _options;
    private readonly ILogger<WcfCustomsGateway> _logger;
    private readonly Lazy<ChannelFactory<IDeclarationService>> _factory;

    public WcfCustomsGateway(IOptions<CustomsOptions> options, ILogger<WcfCustomsGateway> logger)
    {
        _options = options.Value;
        _logger = logger;
        _factory = new Lazy<ChannelFactory<IDeclarationService>>(CreateFactory);
    }

    public async Task<CustomsSubmitResult> SubmitAsync(CustomsDeclaration declaration, Shipment shipment, CancellationToken cancellationToken)
    {
        var request = DeclarationMapper.ToRequest(declaration, shipment, _options.DeclarantEori);
        var response = await CallAsync(c => c.SubmitDeclarationAsync(request));
        _logger.LogInformation("Declaration {Lrn} submitted: accepted={Accepted} MRN={Mrn}", declaration.Lrn, response.Accepted, response.Mrn);
        return new CustomsSubmitResult(
            response.Accepted,
            response.Mrn,
            response.AcceptanceDate is { } a ? new DateTimeOffset(DateTime.SpecifyKind(a, DateTimeKind.Utc)) : null,
            response.Errors.Select(e => $"{e.Code} {e.Pointer}: {e.Text}".Trim()).ToList());
    }

    public async Task<CustomsStatusResult> GetStatusAsync(CustomsDeclaration declaration, CancellationToken cancellationToken)
    {
        var response = await CallAsync(c => c.GetDeclarationStatusAsync(new DeclarationStatusRequest
        {
            DeclarantEori = declaration.DeclarantEori,
            Lrn = declaration.Lrn,
            Mrn = declaration.Mrn
        }));
        var reason = response.Errors.Count > 0
            ? string.Join("; ", response.Errors.Select(e => e.Text))
            : response.ControlType is null ? null : "Control: " + response.ControlType;
        return new CustomsStatusResult(
            DeclarationMapper.MapStatus(response.Status),
            response.Mrn,
            response.StatusDate is { } d ? new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc)) : null,
            reason);
    }

    public void Dispose()
    {
        if (!_factory.IsValueCreated) return;
        try
        {
            _factory.Value.Close();
        }
        catch
        {
            _factory.Value.Abort();
        }
    }

    private async Task<T> CallAsync<T>(Func<IDeclarationService, Task<T>> call)
    {
        var channel = _factory.Value.CreateChannel();
        var comm = (ICommunicationObject)channel;
        try
        {
            var result = await call(channel);
            comm.Close();
            return result;
        }
        catch
        {
            comm.Abort();
            throw;
        }
    }

    private ChannelFactory<IDeclarationService> CreateFactory()
    {
        var endpoint = new Uri(_options.Endpoint);
        var timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        if (endpoint.Scheme == Uri.UriSchemeHttp)
        {
            // lab stub only; the customs service itself requires TLS with a client certificate
            _logger.LogWarning("Customs endpoint {Endpoint} is plain HTTP, no client certificate is sent", endpoint);
            var plain = new BasicHttpBinding(BasicHttpSecurityMode.None) { MaxReceivedMessageSize = 4 * 1024 * 1024, SendTimeout = timeout, ReceiveTimeout = timeout };
            return new ChannelFactory<IDeclarationService>(plain, new EndpointAddress(endpoint));
        }

        var binding = new BasicHttpsBinding(BasicHttpsSecurityMode.Transport)
        {
            MaxReceivedMessageSize = 4 * 1024 * 1024,
            SendTimeout = timeout,
            ReceiveTimeout = timeout
        };
        binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Certificate;
        var factory = new ChannelFactory<IDeclarationService>(binding, new EndpointAddress(endpoint));
        factory.Credentials.ClientCertificate.Certificate = LoadCertificate();
        return factory;
    }

    private X509Certificate2 LoadCertificate()
    {
        var thumbprint = (_options.CertificateThumbprint ?? "").Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        if (thumbprint.Length == 0) throw new InvalidOperationException("Customs:CertificateThumbprint is not configured.");
        var location = Enum.Parse<StoreLocation>(_options.CertificateStoreLocation, ignoreCase: true);
        using var store = new X509Store(StoreName.My, location);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
        if (found.Count == 0)
            throw new InvalidOperationException($"Customs client certificate {thumbprint} not found in {location}\\My.");
        var cert = found[0];
        if (!cert.HasPrivateKey) throw new InvalidOperationException($"Customs client certificate {thumbprint} has no private key.");
        if (cert.NotAfter < DateTime.Now.AddDays(30))
            _logger.LogWarning("Customs client certificate {Thumbprint} expires on {NotAfter:yyyy-MM-dd}", thumbprint, cert.NotAfter);
        return cert;
    }
}
