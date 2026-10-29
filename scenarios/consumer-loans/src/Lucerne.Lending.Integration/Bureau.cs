using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lucerne.Lending.Integration;

public sealed class BureauOptions
{
    public const string Section = "Bureau";
    public string Endpoint { get; set; } = "https://ws.nordiccredit.example/consumer/v3/ConsumerReportService.svc";
    public string UserId { get; set; } = "";
    public string Password { get; set; } = "";
    public string CustomerCode { get; set; } = "LUCSB";
    /// <summary>Purpose code 2 = credit decision (Finnish Credit Information Act 7 §).</summary>
    public string PurposeCode { get; set; } = "2";
    public int TimeoutSeconds { get; set; } = 20;
}

public static class BureauNamespaces
{
    public const string Service = "http://schemas.nordiccredit.example/consumer/v3";
}

[XmlType(Namespace = BureauNamespaces.Service)]
public sealed class ConsumerReportRequest
{
    public string UserId { get; set; } = "";
    public string Password { get; set; } = "";
    public string CustomerCode { get; set; } = "";
    public string PersonId { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Language { get; set; } = "FI";
}

[XmlType(Namespace = BureauNamespaces.Service)]
public sealed class ConsumerReport
{
    public string ResponseStatus { get; set; } = "";
    public string RequestId { get; set; } = "";
    public string PersonId { get; set; } = "";
    public string Name { get; set; } = "";
    public bool PersonFound { get; set; }
    public bool Deceased { get; set; }
    public bool CreditBlock { get; set; }
    public bool DebtRestructuring { get; set; }
    public int PaymentDefaultCount { get; set; }
    [XmlArrayItem("Default")]
    public List<PaymentDefault> PaymentDefaults { get; set; } = new();
    public int Score { get; set; }
    public string ScoreBand { get; set; } = "";
    public int CreditInquiries12M { get; set; }
    public int ReportedLoans { get; set; }
    public string? Message { get; set; }
}

[XmlType(Namespace = BureauNamespaces.Service)]
public sealed class PaymentDefault
{
    public DateTime RegisteredDate { get; set; }
    public string Creditor { get; set; } = "";
    public string Type { get; set; } = "";
    public decimal Amount { get; set; }
    public string Source { get; set; } = "";
}

[ServiceContract(Namespace = BureauNamespaces.Service, Name = "ConsumerReportService")]
[XmlSerializerFormat]
public interface IConsumerReportService
{
    [OperationContract(Action = BureauNamespaces.Service + "/ConsumerReportService/GetConsumerReport")]
    Task<GetConsumerReportResponse> GetConsumerReportAsync(GetConsumerReportRequest request);
}

[MessageContract(WrapperName = "GetConsumerReport", WrapperNamespace = BureauNamespaces.Service, IsWrapped = true)]
public sealed class GetConsumerReportRequest
{
    [MessageBodyMember(Namespace = BureauNamespaces.Service, Order = 0)]
    public ConsumerReportRequest Request { get; set; } = new();
}

[MessageContract(WrapperName = "GetConsumerReportResponse", WrapperNamespace = BureauNamespaces.Service, IsWrapped = true)]
public sealed class GetConsumerReportResponse
{
    [MessageBodyMember(Namespace = BureauNamespaces.Service, Order = 0)]
    public ConsumerReport Report { get; set; } = new();
}

public interface ICreditBureau
{
    Task<(ConsumerReport Report, string RawXml)> GetReportAsync(string personalIdentityCode, string reference, CancellationToken ct = default);
}

/// <summary>Nordic Credit consumer report service (SOAP 1.1).</summary>
public sealed class CreditBureauClient : ICreditBureau, IDisposable
{
    private readonly BureauOptions _options;
    private readonly ILogger<CreditBureauClient> _log;
    private readonly ChannelFactory<IConsumerReportService> _factory;

    public CreditBureauClient(IOptions<BureauOptions> options, ILogger<CreditBureauClient> log)
    {
        _options = options.Value;
        _log = log;
        var uri = new Uri(_options.Endpoint);
        var binding = new BasicHttpBinding(uri.Scheme == "https" ? BasicHttpSecurityMode.Transport : BasicHttpSecurityMode.None)
        {
            MaxReceivedMessageSize = 4 * 1024 * 1024,
            SendTimeout = TimeSpan.FromSeconds(_options.TimeoutSeconds),
            ReceiveTimeout = TimeSpan.FromSeconds(_options.TimeoutSeconds)
        };
        _factory = new ChannelFactory<IConsumerReportService>(binding, new EndpointAddress(uri));
    }

    public async Task<(ConsumerReport Report, string RawXml)> GetReportAsync(string personalIdentityCode, string reference, CancellationToken ct = default)
    {
        var channel = _factory.CreateChannel();
        try
        {
            var response = await channel.GetConsumerReportAsync(new GetConsumerReportRequest
            {
                Request = new ConsumerReportRequest
                {
                    UserId = _options.UserId,
                    Password = _options.Password,
                    CustomerCode = _options.CustomerCode,
                    PersonId = personalIdentityCode.ToUpperInvariant(),
                    Purpose = _options.PurposeCode,
                    Reference = reference
                }
            });
            ((IClientChannel)channel).Close();
            var report = response.Report;
            if (report.ResponseStatus != "OK")
                throw new BureauException($"Bureau returned {report.ResponseStatus}: {report.Message}");
            _log.LogInformation("Bureau report {RequestId} for {Reference}: score {Score}, defaults {Defaults}", report.RequestId, reference,
                report.Score, report.PaymentDefaultCount);
            return (report, Serialize(report));
        }
        catch (CommunicationException ex)
        {
            ((IClientChannel)channel).Abort();
            throw new BureauException("Credit bureau is not available: " + ex.Message, ex);
        }
        catch (TimeoutException ex)
        {
            ((IClientChannel)channel).Abort();
            throw new BureauException("Credit bureau timed out.", ex);
        }
    }

    private static string Serialize(ConsumerReport report)
    {
        var serializer = new XmlSerializer(typeof(ConsumerReport));
        using var writer = new StringWriter();
        serializer.Serialize(writer, report);
        return writer.ToString();
    }

    public void Dispose()
    {
        if (_factory.State == CommunicationState.Opened) _factory.Close();
    }
}

public sealed class BureauException : Exception
{
    public BureauException(string message, Exception? inner = null) : base(message, inner) { }
}
