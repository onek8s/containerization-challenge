using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml;

namespace CityPermits.Integration;

/// <summary>Recordia RecordsOne 5 capture service (RecordService.asmx, SOAP 1.1).</summary>
[ServiceContract(Namespace = "http://recordia.com/recordsone/2015/capture")]
public interface IRecordCaptureService
{
    [OperationContract(Action = "http://recordia.com/recordsone/2015/capture/CaptureByReference")]
    Task<CaptureResponse> CaptureByReferenceAsync(CaptureRequest request);

    [OperationContract(Action = "http://recordia.com/recordsone/2015/capture/GetRecord")]
    Task<RecordInfo> GetRecordAsync(string recordNumber);
}

[MessageContract(WrapperName = "CaptureByReference", WrapperNamespace = "http://recordia.com/recordsone/2015/capture")]
public sealed class CaptureRequest
{
    [MessageBodyMember(Order = 1)] public string Container { get; set; } = "";
    [MessageBodyMember(Order = 2)] public string Title { get; set; } = "";
    [MessageBodyMember(Order = 3)] public string RecordType { get; set; } = "";
    /// <summary>UNC path the records server reads the file from (captured in place, not copied).</summary>
    [MessageBodyMember(Order = 4)] public string SourcePath { get; set; } = "";
    [MessageBodyMember(Order = 5)] public string RetentionSchedule { get; set; } = "";
    [MessageBodyMember(Order = 6)] public string ExternalId { get; set; } = "";
    [MessageBodyMember(Order = 7)] public string Sha256 { get; set; } = "";
    [MessageBodyMember(Order = 8)] public string SecurityLevel { get; set; } = "OFFICIAL";
}

[MessageContract(WrapperName = "CaptureByReferenceResponse", WrapperNamespace = "http://recordia.com/recordsone/2015/capture")]
public sealed class CaptureResponse
{
    [MessageBodyMember(Order = 1)] public string RecordNumber { get; set; } = "";
    [MessageBodyMember(Order = 2)] public bool Duplicate { get; set; }
}

public sealed class RecordInfo
{
    public string RecordNumber { get; set; } = "";
    public string Container { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime Captured { get; set; }
}

public sealed class RecordsOptions
{
    public string Endpoint { get; set; } = "http://records01.corp.local/RecordsOne/RecordService.asmx";
    public string ContainerPrefix { get; set; } = "PLN/PERMITS";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public static class RecordsClientFactory
{
    public static IRecordCaptureService Create(RecordsOptions options)
    {
        var binding = new BasicHttpBinding(options.Endpoint.StartsWith("https", StringComparison.OrdinalIgnoreCase)
            ? BasicHttpSecurityMode.Transport : BasicHttpSecurityMode.TransportCredentialOnly)
        {
            MaxReceivedMessageSize = 4 * 1024 * 1024,
            ReaderQuotas = XmlDictionaryReaderQuotas.Max,
            SendTimeout = TimeSpan.FromSeconds(60)
        };
        binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;
        var factory = new ChannelFactory<IRecordCaptureService>(binding, new EndpointAddress(options.Endpoint));
        factory.Credentials.UserName.UserName = options.Username;
        factory.Credentials.UserName.Password = options.Password;
        return factory.CreateChannel();
    }

    public static string RetentionFor(string documentKind) => documentKind switch
    {
        "DecisionLetter" or "PermitCertificate" => "PLN-DEC-PERM",
        "Invoice" => "FIN-INV-7Y",
        "InspectionReport" => "BC-INS-15Y",
        _ => "PLN-CASE-6Y"
    };
}
