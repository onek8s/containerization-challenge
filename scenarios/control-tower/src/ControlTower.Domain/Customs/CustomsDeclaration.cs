namespace Proseware.ControlTower.Domain.Customs;

public enum DeclarationType
{
    Export = 1,
    Import = 2,
    Transit = 3
}

public enum DeclarationStatus
{
    Draft = 0,
    Submitted = 1,
    Accepted = 2,
    UnderControl = 3,
    Released = 4,
    Rejected = 5,
    Cancelled = 6
}

public class CustomsDeclaration
{
    public int Id { get; set; }
    public int ShipmentId { get; set; }
    public DeclarationType Type { get; set; }
    public DeclarationStatus Status { get; set; }
    /// <summary>Local reference number sent with the declaration.</summary>
    public string Lrn { get; set; } = "";
    /// <summary>Movement reference number given by customs on acceptance.</summary>
    public string? Mrn { get; set; }
    public string DeclarantEori { get; set; } = "";
    public string? ConsignorEori { get; set; }
    public string? ConsigneeEori { get; set; }
    public string OfficeOfDeclaration { get; set; } = "";
    public string? OfficeOfExit { get; set; }
    public string CurrencyCode { get; set; } = "EUR";
    public string? RejectionReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public int SubmitAttempts { get; set; }
    public List<DeclarationLine> Lines { get; set; } = new();

    public decimal TotalStatisticalValue => Lines.Sum(l => l.StatisticalValue);

    public decimal TotalGrossMass => Lines.Sum(l => l.GrossMassKg);
}

public class DeclarationLine
{
    public int Id { get; set; }
    public int CustomsDeclarationId { get; set; }
    public int LineNumber { get; set; }
    /// <summary>Combined nomenclature code (8 digits) or TARIC (10 digits).</summary>
    public string CommodityCode { get; set; } = "";
    public string Description { get; set; } = "";
    public string OriginCountry { get; set; } = "";
    public decimal NetMassKg { get; set; }
    public decimal GrossMassKg { get; set; }
    public decimal StatisticalValue { get; set; }
    public string ProcedureCode { get; set; } = "1000";
    public int Packages { get; set; }
    public string PackageKindCode { get; set; } = "PX";
}
