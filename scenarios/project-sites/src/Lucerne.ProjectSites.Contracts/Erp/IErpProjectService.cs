using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace Lucerne.ProjectSites.Contracts.Erp
{
    /// <summary>
    /// Project accounting web service of the ERP (erp01, /ProjectAccounting/ProjectService.svc).
    /// Contract taken from the WSDL published by the ERP integration team, version 2016-03.
    /// </summary>
    [ServiceContract(Namespace = ErpNamespaces.Service, Name = "ProjectService")]
    public interface IErpProjectService
    {
        [OperationContract]
        [FaultContract(typeof(ErpFault))]
        ErpProjectPage GetProjectsChangedSince(DateTime sinceUtc, int page, int pageSize);

        [OperationContract]
        [FaultContract(typeof(ErpFault))]
        ErpProject GetProject(string projectNumber);

        [OperationContract]
        [FaultContract(typeof(ErpFault))]
        List<ErpTimesheetLine> GetApprovedTimesheetLines(DateTime fromDate, DateTime toDate, string projectNumber);
    }

    public static class ErpNamespaces
    {
        public const string Service = "http://schemas.lucerne-eng.example/erp/projects/2016/03";
        public const string Data = "http://schemas.lucerne-eng.example/erp/projects/2016/03/data";
    }

    [DataContract(Namespace = ErpNamespaces.Data)]
    public class ErpProjectPage
    {
        [DataMember(Order = 1)]
        public List<ErpProject> Projects { get; set; }

        [DataMember(Order = 2)]
        public int Page { get; set; }

        [DataMember(Order = 3)]
        public int TotalCount { get; set; }

        [DataMember(Order = 4)]
        public bool HasMore { get; set; }

        [DataMember(Order = 5)]
        public DateTime ServerTimeUtc { get; set; }
    }

    [DataContract(Namespace = ErpNamespaces.Data)]
    public class ErpProject
    {
        [DataMember(Order = 1)] public string ProjectNo { get; set; }
        [DataMember(Order = 2)] public string Description { get; set; }
        [DataMember(Order = 3)] public string CustomerNo { get; set; }
        [DataMember(Order = 4)] public string CustomerName { get; set; }
        [DataMember(Order = 5)] public string ProfitCentre { get; set; }
        [DataMember(Order = 6)] public string MarketSector { get; set; }

        /// <summary>ERP status codes: 10 Prospect, 20 Open, 30 Suspended, 40 Final invoicing, 50 Closed, 90 Lost.</summary>
        [DataMember(Order = 7)] public int StatusCode { get; set; }

        [DataMember(Order = 8)] public string ManagerEmployeeNo { get; set; }
        [DataMember(Order = 9)] public string DirectorEmployeeNo { get; set; }
        [DataMember(Order = 10)] public DateTime? StartDate { get; set; }
        [DataMember(Order = 11)] public DateTime? EndDate { get; set; }
        [DataMember(Order = 12)] public DateTime? ClosedDate { get; set; }
        [DataMember(Order = 13)] public decimal BudgetHours { get; set; }
        [DataMember(Order = 14)] public decimal ContractValue { get; set; }
        [DataMember(Order = 15)] public string CurrencyCode { get; set; }
        [DataMember(Order = 16)] public decimal PercentComplete { get; set; }
        [DataMember(Order = 17)] public bool Restricted { get; set; }
        [DataMember(Order = 18)] public string DisciplineCodes { get; set; }
        [DataMember(Order = 19)] public DateTime LastChangedUtc { get; set; }
    }

    [DataContract(Namespace = ErpNamespaces.Data)]
    public class ErpTimesheetLine
    {
        [DataMember(Order = 1)] public long LineId { get; set; }
        [DataMember(Order = 2)] public string EmployeeNo { get; set; }
        [DataMember(Order = 3)] public string ProjectNo { get; set; }
        [DataMember(Order = 4)] public string ActivityCode { get; set; }
        [DataMember(Order = 5)] public DateTime WorkDate { get; set; }
        [DataMember(Order = 6)] public decimal Quantity { get; set; }
        [DataMember(Order = 7)] public string ApprovalState { get; set; }
    }

    [DataContract(Namespace = ErpNamespaces.Data)]
    public class ErpFault
    {
        [DataMember(Order = 1)] public string Code { get; set; }
        [DataMember(Order = 2)] public string Message { get; set; }
    }
}
