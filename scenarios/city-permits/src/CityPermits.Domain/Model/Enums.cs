namespace CityPermits.Domain.Model;

public enum PermitKind
{
    BuildingPermit = 1,
    BusinessLicence = 2,
    ParkingPermit = 3
}

public enum CaseStatus
{
    Draft = 0,
    AwaitingPayment = 10,
    Submitted = 20,
    IntakeCheck = 30,
    InformationRequested = 40,
    UnderReview = 50,
    Consultation = 60,
    DecisionPending = 70,
    Approved = 80,
    Refused = 90,
    Issued = 100,
    Withdrawn = 110,
    Expired = 120,
    Revoked = 130
}

public enum ApplicantType
{
    Person = 1,
    Organisation = 2
}

public enum Channel
{
    Online = 1,
    Paper = 2,
    Counter = 3
}

public enum DecisionOutcome
{
    Approved = 1,
    ApprovedWithConditions = 2,
    Refused = 3
}

public enum PaymentStatus
{
    Pending = 0,
    Authorised = 1,
    Settled = 2,
    Failed = 3,
    Refunded = 4,
    Cancelled = 5
}

public enum InspectionStatus
{
    Requested = 0,
    Scheduled = 1,
    InProgress = 2,
    Passed = 3,
    PassedWithRemarks = 4,
    Failed = 5,
    Cancelled = 6,
    NoAccess = 7
}

public enum DocumentKind
{
    Attachment = 1,
    DecisionLetter = 2,
    PermitCertificate = 3,
    ParkingPermit = 4,
    InspectionReport = 5,
    Invoice = 6,
    ReminderLetter = 7,
    RenewalNotice = 8,
    ScannedApplication = 9,
    SiteMap = 10
}

public enum WorkType
{
    NewBuilding = 1,
    Extension = 2,
    Alteration = 3,
    Demolition = 4,
    ChangeOfUse = 5,
    Outbuilding = 6,
    Signage = 7
}

public enum BusinessCategory
{
    FoodPremises = 1,
    AlcoholOnSale = 2,
    AlcoholOffSale = 3,
    TaxiVehicle = 4,
    StreetTrading = 5,
    Entertainment = 6,
    ScrapMetalDealer = 7,
    HairAndBeauty = 8,
    AnimalBoarding = 9
}

public enum VehicleEmissionBand
{
    Zero = 0,
    Low = 1,
    Standard = 2,
    High = 3
}

public enum ParkingPermitType
{
    Resident = 1,
    Visitor = 2,
    Business = 3,
    Carer = 4
}
