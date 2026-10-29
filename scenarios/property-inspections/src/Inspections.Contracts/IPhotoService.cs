using System;
using System.ServiceModel;

namespace Northwind.Inspections.Contracts
{
    [ServiceContract(Namespace = Namespaces.Service, Name = "PhotoService")]
    public interface IPhotoService
    {
        [OperationContract]
        [FaultContract(typeof(InspectionFault))]
        PhotoDto UploadPhoto(UploadPhotoRequest request);

        [OperationContract]
        PhotoDto[] GetPhotos(int inspectionId);

        [OperationContract]
        [FaultContract(typeof(InspectionFault))]
        void UpdateCaption(Guid photoId, string caption, string changedBy);

        [OperationContract]
        [FaultContract(typeof(InspectionFault))]
        void DeletePhoto(Guid photoId, string deletedBy);

        [OperationContract]
        [FaultContract(typeof(InspectionFault))]
        long RequestReport(int inspectionId, string requestedBy);

        [OperationContract]
        ServiceStatusDto GetStatus();
    }

    [ServiceContract(Namespace = Namespaces.Partner, Name = "ClaimStatusService")]
    public interface IClaimStatusService
    {
        [OperationContract]
        [FaultContract(typeof(InspectionFault))]
        ClaimInspectionDto[] GetInspections(string claimNumber);

        [OperationContract]
        [FaultContract(typeof(InspectionFault))]
        byte[] GetContactSheet(int inspectionId);
    }

    public static class Namespaces
    {
        public const string Service = "http://schemas.northwind-insurance.example/inspections/2019/03";
        public const string Partner = "http://schemas.northwind-insurance.example/inspections/partner/2020/11";
        public const string Data = "http://schemas.northwind-insurance.example/inspections/data/2019/03";
    }
}
