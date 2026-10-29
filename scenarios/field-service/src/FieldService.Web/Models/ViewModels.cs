using System.ComponentModel.DataAnnotations;
using FieldService.Data.Entities;
using FieldService.Web.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FieldService.Web.Models;

public class LoginViewModel
{
    [Required]
    [Display(Name = "User name")]
    public string UserName { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public class DashboardViewModel
{
    public Dictionary<WorkOrderStatus, int> StatusCounts { get; set; } = new();

    public int OverdueCount { get; set; }

    public List<ImportedFile> RecentImports { get; set; } = new();

    public List<InvoiceBatch> RecentBatches { get; set; } = new();

    public int Count(WorkOrderStatus status) => StatusCounts.TryGetValue(status, out var c) ? c : 0;
}

public class BoardViewModel
{
    public BoardFilter Filter { get; set; } = new();

    public List<WorkOrder> Orders { get; set; } = new();

    public List<SelectListItem> Technicians { get; set; } = new();
}

public class WorkOrderDetailsViewModel
{
    public WorkOrder Order { get; set; } = null!;

    public List<SelectListItem> Technicians { get; set; } = new();
}

public class CreateWorkOrderViewModel
{
    [Required]
    [Display(Name = "Customer")]
    public string CustomerAccount { get; set; } = "";

    [Required]
    [StringLength(300)]
    [Display(Name = "Site address")]
    public string SiteAddress { get; set; } = "";

    [Required]
    [StringLength(200)]
    public string Summary { get; set; } = "";

    [StringLength(4000)]
    public string? Description { get; set; }

    public WorkOrderPriority Priority { get; set; } = WorkOrderPriority.Normal;

    [StringLength(50)]
    [Display(Name = "Customer reference")]
    public string? CustomerReference { get; set; }
}

public class CompleteWorkOrderViewModel
{
    public string Number { get; set; } = "";

    public string? Summary { get; set; }

    [Range(0.25, 24)]
    [Display(Name = "Labor hours")]
    public decimal LaborHours { get; set; }

    [StringLength(4000)]
    [Display(Name = "Resolution notes")]
    public string? ResolutionNotes { get; set; }

    public List<PartLine> Parts { get; set; } = Enumerable.Range(0, 4).Select(_ => new PartLine()).ToList();
}

public class PartLine
{
    public string? PartNumber { get; set; }

    public string? Description { get; set; }

    [Range(0, 999)]
    public int Quantity { get; set; }

    [Range(0, 100000)]
    public decimal UnitPrice { get; set; }
}

public class CustomerRow
{
    public Customer Customer { get; set; } = null!;

    public int OpenOrders { get; set; }

    public int TotalOrders { get; set; }
}
