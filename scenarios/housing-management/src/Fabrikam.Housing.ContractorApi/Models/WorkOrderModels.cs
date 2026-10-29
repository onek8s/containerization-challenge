using System;
using System.Collections.Generic;
using Fabrikam.Housing.Repairs;

namespace Fabrikam.Housing.ContractorApi.Models
{
    public class PagedResult<T>
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public IList<T> Items { get; set; }
    }

    public class WorkOrderSummary
    {
        public string Reference { get; set; }
        public WorkOrderStatus Status { get; set; }
        public RepairPriority Priority { get; set; }
        public Trade Trade { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
        public DateTime ReportedAt { get; set; }
        public DateTime TargetDate { get; set; }
        public string PropertyReference { get; set; }
        public string Address { get; set; }
        public string Postcode { get; set; }
    }

    public class WorkOrderDetail : WorkOrderSummary
    {
        public string Uprn { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int NoAccessCount { get; set; }
        public bool AsbestosPresent { get; set; }
        public ContactModel Contact { get; set; }
        public IList<LineModel> Lines { get; set; }
        public IList<AppointmentModel> Appointments { get; set; }
    }

    public class ContactModel
    {
        public string Name { get; set; }
        public string MobilePhone { get; set; }
        public string Note { get; set; }
    }

    public class LineModel
    {
        public string SorCode { get; set; }
        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
        public bool AddedByContractor { get; set; }
    }

    public class AppointmentModel
    {
        public DateTime Date { get; set; }
        public AppointmentSlot Slot { get; set; }
        public bool Cancelled { get; set; }
        public string Outcome { get; set; }
    }

    public class RejectRequest
    {
        public string Reason { get; set; }
    }

    public class AppointmentRequest
    {
        public DateTime Date { get; set; }
        public AppointmentSlot Slot { get; set; }
    }

    public class NoAccessRequest
    {
        public string Outcome { get; set; }
    }

    public class CompletionLine
    {
        public string SorCode { get; set; }
        public decimal Quantity { get; set; }
    }

    public class CompleteRequest
    {
        public string Notes { get; set; }
        public decimal LabourHours { get; set; }
        public List<CompletionLine> ExtraLines { get; set; } = new List<CompletionLine>();
        public bool SuggestRecharge { get; set; }
        public string RechargeReason { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class PhotoResult
    {
        public Guid DocumentKey { get; set; }
        public string FileName { get; set; }
    }

    public class ScheduleOfRatesModel
    {
        public string Code { get; set; }
        public string Description { get; set; }
        public Trade Trade { get; set; }
        public RepairPriority DefaultPriority { get; set; }
        public decimal UnitPrice { get; set; }
        public string Unit { get; set; }
    }
}
