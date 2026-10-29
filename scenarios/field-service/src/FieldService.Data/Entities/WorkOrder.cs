namespace FieldService.Data.Entities;

public enum WorkOrderStatus
{
    New = 0,
    Assigned = 1,
    InProgress = 2,
    Completed = 3,
    Invoiced = 4,
    Cancelled = 9
}

public enum WorkOrderPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Emergency = 3
}

public class WorkOrder
{
    public int Id { get; set; }

    public string Number { get; set; } = "";

    public int CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public string SiteAddress { get; set; } = "";

    public string Summary { get; set; } = "";

    public string? Description { get; set; }

    public WorkOrderPriority Priority { get; set; } = WorkOrderPriority.Normal;

    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.New;

    public int? TechnicianId { get; set; }

    public Technician? Technician { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime DueUtc { get; set; }

    public DateTime? CompletedUtc { get; set; }

    public decimal LaborHours { get; set; }

    public string? ResolutionNotes { get; set; }

    public string Source { get; set; } = "Portal";

    public string? CustomerReference { get; set; }

    public string? InvoiceBatch { get; set; }

    public List<WorkOrderPart> Parts { get; set; } = new();

    public List<WorkOrderPhoto> Photos { get; set; } = new();

    public bool IsOpen => Status is WorkOrderStatus.New or WorkOrderStatus.Assigned or WorkOrderStatus.InProgress;

    public bool IsOverdue(DateTime nowUtc) => IsOpen && DueUtc < nowUtc;
}

public class WorkOrderPart
{
    public int Id { get; set; }

    public int WorkOrderId { get; set; }

    public string PartNumber { get; set; } = "";

    public string Description { get; set; } = "";

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}

public class WorkOrderPhoto
{
    public int Id { get; set; }

    public int WorkOrderId { get; set; }

    public string OriginalFileName { get; set; } = "";

    public string StoredFileName { get; set; } = "";

    public string ContentType { get; set; } = "application/octet-stream";

    public DateTime UploadedUtc { get; set; }

    public string UploadedBy { get; set; } = "";
}
