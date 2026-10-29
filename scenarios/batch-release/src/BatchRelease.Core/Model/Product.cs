namespace BatchRelease.Core.Model;

public class Product
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string FamilyCode { get; set; } = "";
    public ProductFamily? Family { get; set; }
    public string Strength { get; set; } = "";
    public string DosageForm { get; set; } = "";
    public int ShelfLifeMonths { get; set; }

    /// <summary>Shelf life that must remain on the day of release.</summary>
    public int MinimumRemainingMonths { get; set; }
    public bool Active { get; set; } = true;
}

public class ProductFamily
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>
    /// Release policy for batches of this family, as an SDDL security descriptor whose
    /// access mask bits are <see cref="Security.ReleaseRights"/>.
    /// </summary>
    public string PolicySddl { get; set; } = "";
    public DateTime PolicyChangedUtc { get; set; }
    public string PolicyChangedBy { get; set; } = "";
}
