using System.ComponentModel.DataAnnotations;

namespace PartsCatalog.Models;

public class Part
{
    public int Id { get; set; }

    [Required, StringLength(32)]
    [Display(Name = "Part number")]
    public string PartNumber { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Description { get; set; } = string.Empty;

    [StringLength(64)]
    public string? Category { get; set; }

    [Range(0, int.MaxValue)]
    [Display(Name = "Qty on hand")]
    public int QuantityOnHand { get; set; }

    [Range(0, int.MaxValue)]
    [Display(Name = "Reorder level")]
    public int ReorderLevel { get; set; }

    [Range(0, 1_000_000)]
    [DataType(DataType.Currency)]
    [Display(Name = "Unit price")]
    public decimal UnitPrice { get; set; }

    [StringLength(16)]
    [Display(Name = "Bin location")]
    public string? BinLocation { get; set; }

    public DateTime LastUpdated { get; set; }

    public bool NeedsReorder => QuantityOnHand <= ReorderLevel;
}
