using System;
using Fabrikam.Housing.Compliance;
using Fabrikam.Housing.Contractors;
using Microsoft.AspNetCore.Http;

namespace Fabrikam.Housing.ContractorApi.Models
{
    public class InvoiceRequest
    {
        public string InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public string WorkOrderReference { get; set; }
        public decimal NetAmount { get; set; }
        public decimal VatAmount { get; set; }
    }

    public class InvoiceModel
    {
        public string InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public string WorkOrderReference { get; set; }
        public decimal NetAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal GrossAmount { get; set; }
        public InvoiceStatus Status { get; set; }
        public string QueryReason { get; set; }
        public DateTime? ApprovedAt { get; set; }
    }

    public class CertificateRequest
    {
        public string PropertyReference { get; set; }
        public ComplianceType Type { get; set; }
        public string CertificateNumber { get; set; }
        public DateTime InspectedOn { get; set; }
        public CertificateOutcome Outcome { get; set; }
        public string EngineerName { get; set; }
        public string EngineerRegistration { get; set; }
        public string Observations { get; set; }
        public IFormFile File { get; set; }
    }

    public class CertificateModel
    {
        public string PropertyReference { get; set; }
        public ComplianceType Type { get; set; }
        public string CertificateNumber { get; set; }
        public DateTime InspectedOn { get; set; }
        public DateTime ExpiresOn { get; set; }
        public CertificateOutcome Outcome { get; set; }
        public string RemedialWorkOrder { get; set; }
        public Guid? DocumentKey { get; set; }
    }
}
