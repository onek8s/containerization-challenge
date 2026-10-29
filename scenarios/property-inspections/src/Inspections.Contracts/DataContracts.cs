using System;
using System.Runtime.Serialization;

namespace Northwind.Inspections.Contracts
{
    [DataContract(Namespace = Namespaces.Data)]
    public class UploadPhotoRequest
    {
        [DataMember(IsRequired = true, Order = 0)] public int InspectionId { get; set; }
        [DataMember(IsRequired = true, Order = 1)] public string FileName { get; set; }
        [DataMember(Order = 2)] public string Caption { get; set; }
        [DataMember(IsRequired = true, Order = 3)] public byte[] Content { get; set; }
        [DataMember(IsRequired = true, Order = 4)] public string UploadedBy { get; set; }
    }

    [DataContract(Namespace = Namespaces.Data)]
    public class PhotoDto
    {
        [DataMember(Order = 0)] public Guid Id { get; set; }
        [DataMember(Order = 1)] public int InspectionId { get; set; }
        [DataMember(Order = 2)] public int Sequence { get; set; }
        [DataMember(Order = 3)] public string FileName { get; set; }
        [DataMember(Order = 4)] public string Caption { get; set; }
        [DataMember(Order = 5)] public int Width { get; set; }
        [DataMember(Order = 6)] public int Height { get; set; }
        [DataMember(Order = 7)] public long Bytes { get; set; }
        [DataMember(Order = 8)] public DateTime? TakenUtc { get; set; }
        [DataMember(Order = 9)] public DateTime UploadedUtc { get; set; }
        [DataMember(Order = 10)] public string UploadedBy { get; set; }
        [DataMember(Order = 11)] public bool Purged { get; set; }
    }

    [DataContract(Namespace = Namespaces.Data)]
    public class ClaimInspectionDto
    {
        [DataMember(Order = 0)] public int InspectionId { get; set; }
        [DataMember(Order = 1)] public string ClaimNumber { get; set; }
        [DataMember(Order = 2)] public string Status { get; set; }
        [DataMember(Order = 3)] public DateTime InspectedOn { get; set; }
        [DataMember(Order = 4)] public string Adjuster { get; set; }
        [DataMember(Order = 5)] public decimal? EstimatedLoss { get; set; }
        [DataMember(Order = 6)] public int PhotoCount { get; set; }
        [DataMember(Order = 7)] public DateTime? ReportSentUtc { get; set; }
    }

    [DataContract(Namespace = Namespaces.Data)]
    public class ServiceStatusDto
    {
        [DataMember(Order = 0)] public string MachineName { get; set; }
        [DataMember(Order = 1)] public string Identity { get; set; }
        [DataMember(Order = 2)] public string Version { get; set; }
        [DataMember(Order = 3)] public bool DatabaseReachable { get; set; }
        [DataMember(Order = 4)] public bool ShareReachable { get; set; }
        [DataMember(Order = 5)] public int QueuedReports { get; set; }
    }

    [DataContract(Namespace = Namespaces.Data)]
    public class InspectionFault
    {
        [DataMember(Order = 0)] public string Code { get; set; }
        [DataMember(Order = 1)] public string Message { get; set; }
    }
}
