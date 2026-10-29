namespace CityPermits.Domain.Model;

/// <summary>One application for a permit or licence and everything that happens to it.</summary>
public class PermitCase
{
    public int Id { get; set; }
    public string Reference { get; set; } = "";
    public PermitKind Kind { get; set; }
    public CaseStatus Status { get; set; }
    public Channel Channel { get; set; } = Channel.Online;

    public int ApplicantId { get; set; }
    public Applicant? Applicant { get; set; }

    public string? Uprn { get; set; }
    public string? SiteAddress { get; set; }
    public string? ParcelId { get; set; }
    public double? Easting { get; set; }
    public double? Northing { get; set; }
    public string? Ward { get; set; }

    public string Title { get; set; } = "";
    public string? Description { get; set; }

    public BuildingDetails? Building { get; set; }
    public BusinessDetails? Business { get; set; }
    public ParkingDetails? Parking { get; set; }

    public string? AssignedOfficer { get; set; }
    public string? Team { get; set; }
    public int Priority { get; set; } = 3;

    public DateTime CreatedUtc { get; set; }
    public DateTime? SubmittedUtc { get; set; }
    public DateTime? TargetDecisionDate { get; set; }
    public DateTime? DecidedUtc { get; set; }
    public DateTime? IssuedUtc { get; set; }
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }

    public DecisionOutcome? Outcome { get; set; }
    public string? DecisionNotes { get; set; }
    public string? DecidedBy { get; set; }
    public int? RenewalOfCaseId { get; set; }
    public int RenewalReminderCount { get; set; }

    public string? SpatialCheckJson { get; set; }
    public DateTime? SpatialCheckedUtc { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public List<FeeLine> Fees { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public List<CaseEvent> Events { get; set; } = new();
    public List<CaseDocument> Documents { get; set; } = new();
    public List<Condition> Conditions { get; set; } = new();
    public List<Inspection> Inspections { get; set; } = new();
    public List<Consultation> Consultations { get; set; } = new();

    public decimal TotalFees => Fees.Where(f => !f.Waived).Sum(f => f.Amount);
    public decimal TotalPaid => Payments.Where(p => p.Status == PaymentStatus.Settled || p.Status == PaymentStatus.Authorised).Sum(p => p.Amount)
                              - Payments.Where(p => p.Status == PaymentStatus.Refunded).Sum(p => p.Amount);
    public decimal Balance => TotalFees - TotalPaid;
}

public class Applicant
{
    public int Id { get; set; }
    public ApplicantType Type { get; set; }
    /// <summary>Pseudonymous subject from the eID broker (persistent NameID). Empty for paper applicants.</summary>
    public string? EidSubject { get; set; }
    public string? PersonalIdHash { get; set; }
    public string? CompanyNumber { get; set; }
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? PostalAddress { get; set; }
    public string? Postcode { get; set; }
    public bool PrefersPaper { get; set; }
    public DateTime CreatedUtc { get; set; }
    public List<PermitCase> Cases { get; set; } = new();
}

public class BuildingDetails
{
    public WorkType WorkType { get; set; }
    public decimal EstimatedCost { get; set; }
    public double GrossFloorAreaM2 { get; set; }
    public double FootprintM2 { get; set; }
    public double HeightM { get; set; }
    public int Storeys { get; set; }
    public string? UseClass { get; set; }
    public string? FootprintWkt { get; set; }
    public bool ListedBuilding { get; set; }
    public string? AgentName { get; set; }
    public string? AgentEmail { get; set; }
}

public class BusinessDetails
{
    public BusinessCategory Category { get; set; }
    public string TradingName { get; set; } = "";
    public string? OpeningHours { get; set; }
    public int? Capacity { get; set; }
    public string? ResponsiblePerson { get; set; }
    public bool Seasonal { get; set; }
    public string? UseClass { get; set; }
}

public class ParkingDetails
{
    public ParkingPermitType PermitType { get; set; }
    public string? Registration { get; set; }
    public VehicleEmissionBand EmissionBand { get; set; }
    public string? ZoneCode { get; set; }
    public int PermitNumberInHousehold { get; set; } = 1;
    public int DurationMonths { get; set; } = 12;
    public string? PermitSerial { get; set; }
}

public class FeeLine
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal VatRate { get; set; }
    public bool Waived { get; set; }
    public string? WaivedBy { get; set; }
    public string? WaiverReason { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string? InvoiceNumber { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public string ProviderReference { get; set; } = "";
    public string? SessionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public PaymentStatus Status { get; set; }
    public string? CardBrand { get; set; }
    public string? Last4 { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? SettledUtc { get; set; }
    public string? SettlementBatch { get; set; }
    public string? FailureReason { get; set; }
}

public class CaseEvent
{
    public long Id { get; set; }
    public int CaseId { get; set; }
    public DateTime AtUtc { get; set; }
    public string Actor { get; set; } = "";
    public string Type { get; set; } = "";
    public CaseStatus? FromStatus { get; set; }
    public CaseStatus? ToStatus { get; set; }
    public string? Text { get; set; }
    public bool VisibleToApplicant { get; set; }
}

public class CaseDocument
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public DocumentKind Kind { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public long Length { get; set; }
    public string Sha256 { get; set; } = "";
    /// <summary>Key in the document archive (stream_id of the archive row).</summary>
    public Guid ArchiveKey { get; set; }
    public string? ArchivePath { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string CreatedBy { get; set; } = "";
    public bool VisibleToApplicant { get; set; }
    public string? RecordsSystemId { get; set; }
    public DateTime? RecordsFiledUtc { get; set; }
    public int RecordsAttempts { get; set; }
    public string? RecordsError { get; set; }
}

public class Condition
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public string Code { get; set; } = "";
    public string Text { get; set; } = "";
    public int Order { get; set; }
}

public class Consultation
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public string Consultee { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTime SentUtc { get; set; }
    public DateOnly DueDate { get; set; }
    public DateTime? RespondedUtc { get; set; }
    public string? Response { get; set; }
    public bool? Objection { get; set; }
}

public class Inspection
{
    public int Id { get; set; }
    public int CaseId { get; set; }
    public string Stage { get; set; } = "";
    public InspectionStatus Status { get; set; }
    public DateOnly? ScheduledDate { get; set; }
    public string? Slot { get; set; }
    public string? Inspector { get; set; }
    public int? RouteOrder { get; set; }
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public string? ResultNotes { get; set; }
    public string? ChecklistJson { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? ClientMutationId { get; set; }
}

public class ZoneTariff
{
    public string ZoneCode { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal ResidentAnnual { get; set; }
    public decimal BusinessAnnual { get; set; }
    public decimal VisitorBookOf20 { get; set; }
    public int MaxPermitsPerHousehold { get; set; } = 2;
    public bool Active { get; set; } = true;
}

public class SequenceCounter
{
    public string Name { get; set; } = "";
    public int Year { get; set; }
    public int Value { get; set; }
}

public class Holiday
{
    public DateOnly Date { get; set; }
    public string Name { get; set; } = "";
}
