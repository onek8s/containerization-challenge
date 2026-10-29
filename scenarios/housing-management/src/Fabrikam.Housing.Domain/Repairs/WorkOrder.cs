using System;
using System.Collections.Generic;
using Fabrikam.Housing.Assets;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Contractors;

namespace Fabrikam.Housing.Repairs
{
    public enum RepairPriority
    {
        Emergency = 1,
        Urgent = 2,
        Routine = 3,
        Planned = 4
    }

    public enum WorkOrderStatus
    {
        Reported = 1,
        Triaged = 2,
        Assigned = 3,
        Accepted = 4,
        Appointed = 5,
        InProgress = 6,
        NoAccess = 7,
        OnHold = 8,
        Completed = 9,
        Cancelled = 10
    }

    public enum Trade
    {
        Plumbing = 1,
        Electrical = 2,
        Heating = 3,
        Carpentry = 4,
        Roofing = 5,
        Plastering = 6,
        Glazing = 7,
        Drainage = 8,
        DampAndMould = 9,
        General = 10
    }

    public enum ReportChannel
    {
        Portal = 1,
        Phone = 2,
        Staff = 3,
        Inspection = 4
    }

    public enum AppointmentSlot
    {
        AllDay = 0,
        Morning = 1,
        Afternoon = 2,
        SchoolRun = 3
    }

    public class ScheduleOfRatesItem : Entity
    {
        public string Code { get; set; }
        public string Description { get; set; }
        public Trade Trade { get; set; }
        public RepairPriority DefaultPriority { get; set; }
        public decimal UnitPrice { get; set; }
        public string Unit { get; set; }
        public bool Active { get; set; } = true;
    }

    public class WorkOrder : AuditedEntity
    {
        public string Reference { get; set; }
        public int PropertyId { get; set; }
        public Property Property { get; set; }
        public int? TenancyId { get; set; }
        public ReportChannel Channel { get; set; }
        public string ReportedBy { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
        public Trade Trade { get; set; }
        public RepairPriority Priority { get; set; }
        public WorkOrderStatus Status { get; set; }
        public DateTime ReportedAt { get; set; }
        public DateTime TargetDate { get; set; }
        public int? ContractorId { get; set; }
        public Contractor Contractor { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string CompletionNotes { get; set; }
        public decimal? LabourHours { get; set; }
        public bool Recharge { get; set; }
        public string RechargeReason { get; set; }
        public int NoAccessCount { get; set; }
        public int? SatisfactionScore { get; set; }
        public string CancellationReason { get; set; }
        public bool SlaBreachNotified { get; set; }
        public List<WorkOrderLine> Lines { get; set; } = new List<WorkOrderLine>();
        public List<Appointment> Appointments { get; set; } = new List<Appointment>();
        public List<WorkOrderEvent> Events { get; set; } = new List<WorkOrderEvent>();

        public bool IsOpen => Status != WorkOrderStatus.Completed && Status != WorkOrderStatus.Cancelled;
    }

    public class WorkOrderLine : Entity
    {
        public int WorkOrderId { get; set; }
        public string SorCode { get; set; }
        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public bool AddedByContractor { get; set; }

        public decimal LineTotal => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
    }

    public class Appointment : Entity
    {
        public int WorkOrderId { get; set; }
        public DateTime Date { get; set; }
        public AppointmentSlot Slot { get; set; }
        public string BookedBy { get; set; }
        public DateTime BookedAt { get; set; }
        public bool Cancelled { get; set; }
        public string Outcome { get; set; }
    }

    public class WorkOrderEvent : Entity
    {
        public int WorkOrderId { get; set; }
        public DateTime At { get; set; }
        public string Actor { get; set; }
        public WorkOrderStatus? NewStatus { get; set; }
        public string Text { get; set; }
    }
}
