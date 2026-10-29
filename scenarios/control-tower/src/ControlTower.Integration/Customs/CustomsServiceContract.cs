using System.Runtime.Serialization;
using System.ServiceModel;

namespace Proseware.ControlTower.Integration.Customs;

// Client side of the customs declaration web service (message exchange specification v2.3).
// Hand-written from the WSDL; keep namespaces and member order as published.

public static class CustomsNamespaces
{
    public const string Service = "urn:customs.example:declaration:service:v2";
    public const string Data = "urn:customs.example:declaration:data:v2";
}

[ServiceContract(Namespace = CustomsNamespaces.Service, Name = "DeclarationService")]
public interface IDeclarationService
{
    [OperationContract(Action = CustomsNamespaces.Service + "/SubmitDeclaration", ReplyAction = CustomsNamespaces.Service + "/SubmitDeclarationResponse")]
    Task<SubmitDeclarationResponse> SubmitDeclarationAsync(SubmitDeclarationRequest request);

    [OperationContract(Action = CustomsNamespaces.Service + "/GetDeclarationStatus", ReplyAction = CustomsNamespaces.Service + "/GetDeclarationStatusResponse")]
    Task<DeclarationStatusResponse> GetDeclarationStatusAsync(DeclarationStatusRequest request);
}

[DataContract(Namespace = CustomsNamespaces.Data)]
public sealed class SubmitDeclarationRequest
{
    [DataMember(Order = 1, IsRequired = true)] public string MessageId { get; set; } = "";
    [DataMember(Order = 2, IsRequired = true)] public string DeclarationType { get; set; } = "";
    [DataMember(Order = 3, IsRequired = true)] public string Lrn { get; set; } = "";
    [DataMember(Order = 4, IsRequired = true)] public string DeclarantEori { get; set; } = "";
    [DataMember(Order = 5)] public string? RepresentativeEori { get; set; }
    [DataMember(Order = 6)] public string? ConsignorEori { get; set; }
    [DataMember(Order = 7)] public string? ConsigneeEori { get; set; }
    [DataMember(Order = 8, IsRequired = true)] public string OfficeOfDeclaration { get; set; } = "";
    [DataMember(Order = 9)] public string? OfficeOfExit { get; set; }
    [DataMember(Order = 10)] public string? TransportDocument { get; set; }
    [DataMember(Order = 11)] public string? MeansOfTransport { get; set; }
    [DataMember(Order = 12)] public string ModeOfTransportAtBorder { get; set; } = "3";
    [DataMember(Order = 13)] public string Currency { get; set; } = "EUR";
    [DataMember(Order = 14)] public decimal TotalInvoiceAmount { get; set; }
    [DataMember(Order = 15)] public string? DeliveryTerms { get; set; }
    [DataMember(Order = 16)] public string DispatchCountry { get; set; } = "";
    [DataMember(Order = 17)] public string DestinationCountry { get; set; } = "";
    [DataMember(Order = 18, IsRequired = true)] public List<GoodsItem> GoodsItems { get; set; } = new();
}

[DataContract(Namespace = CustomsNamespaces.Data)]
public sealed class GoodsItem
{
    [DataMember(Order = 1)] public int ItemNumber { get; set; }
    [DataMember(Order = 2)] public string CommodityCode { get; set; } = "";
    [DataMember(Order = 3)] public string Description { get; set; } = "";
    [DataMember(Order = 4)] public string OriginCountry { get; set; } = "";
    [DataMember(Order = 5)] public decimal NetMass { get; set; }
    [DataMember(Order = 6)] public decimal GrossMass { get; set; }
    [DataMember(Order = 7)] public decimal StatisticalValue { get; set; }
    [DataMember(Order = 8)] public string ProcedureCode { get; set; } = "";
    [DataMember(Order = 9)] public int NumberOfPackages { get; set; }
    [DataMember(Order = 10)] public string KindOfPackages { get; set; } = "";
}

[DataContract(Namespace = CustomsNamespaces.Data)]
public sealed class SubmitDeclarationResponse
{
    [DataMember(Order = 1)] public string MessageId { get; set; } = "";
    [DataMember(Order = 2)] public bool Accepted { get; set; }
    [DataMember(Order = 3)] public string? Mrn { get; set; }
    [DataMember(Order = 4)] public DateTime? AcceptanceDate { get; set; }
    [DataMember(Order = 5)] public List<FunctionalError> Errors { get; set; } = new();
}

[DataContract(Namespace = CustomsNamespaces.Data)]
public sealed class FunctionalError
{
    [DataMember(Order = 1)] public string Code { get; set; } = "";
    [DataMember(Order = 2)] public string? Pointer { get; set; }
    [DataMember(Order = 3)] public string Text { get; set; } = "";
}

[DataContract(Namespace = CustomsNamespaces.Data)]
public sealed class DeclarationStatusRequest
{
    [DataMember(Order = 1)] public string DeclarantEori { get; set; } = "";
    [DataMember(Order = 2)] public string? Lrn { get; set; }
    [DataMember(Order = 3)] public string? Mrn { get; set; }
}

[DataContract(Namespace = CustomsNamespaces.Data)]
public sealed class DeclarationStatusResponse
{
    [DataMember(Order = 1)] public string? Mrn { get; set; }
    /// <summary>ACCEPTED, UNDER_CONTROL, RELEASED, REJECTED, INVALIDATED.</summary>
    [DataMember(Order = 2)] public string Status { get; set; } = "";
    [DataMember(Order = 3)] public DateTime? StatusDate { get; set; }
    [DataMember(Order = 4)] public string? ControlType { get; set; }
    [DataMember(Order = 5)] public List<FunctionalError> Errors { get; set; } = new();
}
