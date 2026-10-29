-- Southridge Clinic Systems: schema (version 6.4)
-- ASP.NET Membership, Roles and Profile tables (aspnet_*) are created separately with aspnet_regsql.exe -A mrp.
USE SouthridgeClinic;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-------------------------------------------------------------------------------
-- Reference data
-------------------------------------------------------------------------------
CREATE TABLE dbo.Site (
    SiteId          int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Site PRIMARY KEY,
    Code            varchar(10)   NOT NULL CONSTRAINT UQ_Site_Code UNIQUE,
    Name            nvarchar(100) NOT NULL,
    Address1        nvarchar(100) NOT NULL,
    City            nvarchar(50)  NOT NULL,
    State           char(2)       NOT NULL,
    Zip             varchar(10)   NOT NULL,
    Phone           varchar(20)   NULL,
    Fax             varchar(20)   NULL,
    PlaceOfService  char(2)       NOT NULL CONSTRAINT DF_Site_Pos DEFAULT ('11'),
    TaxId           varchar(10)   NULL,
    GroupNpi        char(10)      NULL,
    OpensAt         time(0)       NOT NULL CONSTRAINT DF_Site_Opens DEFAULT ('08:00'),
    ClosesAt        time(0)       NOT NULL CONSTRAINT DF_Site_Closes DEFAULT ('17:00'),
    OnlineBooking   bit           NOT NULL CONSTRAINT DF_Site_Online DEFAULT (1),
    IsActive        bit           NOT NULL CONSTRAINT DF_Site_Active DEFAULT (1)
);
GO

CREATE TABLE dbo.Specialty (
    SpecialtyCode   varchar(10)   NOT NULL CONSTRAINT PK_Specialty PRIMARY KEY,
    Name            nvarchar(60)  NOT NULL,
    TaxonomyCode    varchar(10)   NULL,
    RequiresReferral bit          NOT NULL CONSTRAINT DF_Specialty_Ref DEFAULT (0)
);
GO

CREATE TABLE dbo.Provider (
    ProviderId      int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Provider PRIMARY KEY,
    Npi             char(10)      NOT NULL CONSTRAINT UQ_Provider_Npi UNIQUE,
    FirstName       nvarchar(50)  NOT NULL,
    LastName        nvarchar(50)  NOT NULL,
    Credentials     varchar(20)   NULL,
    SpecialtyCode   varchar(10)   NOT NULL CONSTRAINT FK_Provider_Specialty REFERENCES dbo.Specialty (SpecialtyCode),
    DefaultSiteId   int           NOT NULL CONSTRAINT FK_Provider_Site REFERENCES dbo.Site (SiteId),
    UserName        nvarchar(256) NULL,
    AcceptsNewPatients bit        NOT NULL CONSTRAINT DF_Provider_New DEFAULT (1),
    OnlineBooking   bit           NOT NULL CONSTRAINT DF_Provider_Online DEFAULT (1),
    IsActive        bit           NOT NULL CONSTRAINT DF_Provider_Active DEFAULT (1)
);
GO

CREATE TABLE dbo.AppointmentType (
    AppointmentTypeId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppointmentType PRIMARY KEY,
    Code            varchar(10)   NOT NULL CONSTRAINT UQ_AppointmentType_Code UNIQUE,
    Name            nvarchar(60)  NOT NULL,
    DurationMinutes int           NOT NULL,
    Color           char(7)       NOT NULL CONSTRAINT DF_AppointmentType_Color DEFAULT ('#4a7ab5'),
    NewPatient      bit           NOT NULL CONSTRAINT DF_AppointmentType_New DEFAULT (0),
    AllowOnline     bit           NOT NULL CONSTRAINT DF_AppointmentType_Online DEFAULT (0),
    DefaultCptCode  varchar(5)    NULL,
    SendToHospital  bit           NOT NULL CONSTRAINT DF_AppointmentType_Hosp DEFAULT (0),
    IsActive        bit           NOT NULL CONSTRAINT DF_AppointmentType_Active DEFAULT (1)
);
GO

CREATE TABLE dbo.ScheduleTemplate (
    TemplateId      int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ScheduleTemplate PRIMARY KEY,
    ProviderId      int           NOT NULL CONSTRAINT FK_ScheduleTemplate_Provider REFERENCES dbo.Provider (ProviderId),
    SiteId          int           NOT NULL CONSTRAINT FK_ScheduleTemplate_Site REFERENCES dbo.Site (SiteId),
    DayOfWeek       tinyint       NOT NULL CONSTRAINT CK_ScheduleTemplate_Day CHECK (DayOfWeek BETWEEN 0 AND 6),
    StartTime       time(0)       NOT NULL,
    EndTime         time(0)       NOT NULL,
    SlotMinutes     int           NOT NULL CONSTRAINT DF_ScheduleTemplate_Slot DEFAULT (15),
    AppointmentTypes varchar(200) NULL,
    MaxOverbook     int           NOT NULL CONSTRAINT DF_ScheduleTemplate_Overbook DEFAULT (0),
    EffectiveFrom   date          NOT NULL,
    EffectiveTo     date          NULL,
    CONSTRAINT CK_ScheduleTemplate_Times CHECK (EndTime > StartTime)
);
GO
CREATE INDEX IX_ScheduleTemplate_Provider ON dbo.ScheduleTemplate (ProviderId, DayOfWeek);
GO

CREATE TABLE dbo.ScheduleBlock (
    BlockId         int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ScheduleBlock PRIMARY KEY,
    ProviderId      int           NULL CONSTRAINT FK_ScheduleBlock_Provider REFERENCES dbo.Provider (ProviderId),
    SiteId          int           NULL CONSTRAINT FK_ScheduleBlock_Site REFERENCES dbo.Site (SiteId),
    StartsAt        datetime      NOT NULL,
    EndsAt          datetime      NOT NULL,
    Reason          nvarchar(100) NOT NULL,
    CreatedBy       nvarchar(256) NOT NULL,
    CreatedOn       datetime      NOT NULL CONSTRAINT DF_ScheduleBlock_Created DEFAULT (GETDATE())
);
GO
CREATE INDEX IX_ScheduleBlock_Provider ON dbo.ScheduleBlock (ProviderId, StartsAt);
GO

CREATE TABLE dbo.Payer (
    PayerId         int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Payer PRIMARY KEY,
    Code            varchar(10)   NOT NULL CONSTRAINT UQ_Payer_Code UNIQUE,
    Name            nvarchar(100) NOT NULL,
    PayerType       tinyint       NOT NULL,
    ClaimPayerId    varchar(20)   NOT NULL,
    Address1        nvarchar(100) NULL,
    City            nvarchar(50)  NULL,
    State           char(2)       NULL,
    Zip             varchar(10)   NULL,
    FilingIndicator char(2)       NOT NULL CONSTRAINT DF_Payer_Filing DEFAULT ('CI'),
    TimelyFilingDays int          NOT NULL CONSTRAINT DF_Payer_Timely DEFAULT (90),
    IsActive        bit           NOT NULL CONSTRAINT DF_Payer_Active DEFAULT (1)
);
GO

CREATE TABLE dbo.ProcedureCode (
    CptCode         varchar(5)    NOT NULL CONSTRAINT PK_ProcedureCode PRIMARY KEY,
    Description     nvarchar(200) NOT NULL,
    StandardFee     money         NOT NULL,
    Category        varchar(20)   NOT NULL,
    IsActive        bit           NOT NULL CONSTRAINT DF_ProcedureCode_Active DEFAULT (1)
);
GO

CREATE TABLE dbo.DiagnosisCode (
    Code            varchar(8)    NOT NULL,
    CodeSet         tinyint       NOT NULL,
    Description     nvarchar(250) NOT NULL,
    IsActive        bit           NOT NULL CONSTRAINT DF_DiagnosisCode_Active DEFAULT (1),
    CONSTRAINT PK_DiagnosisCode PRIMARY KEY (Code, CodeSet)
);
GO

CREATE TABLE dbo.FeeSchedule (
    FeeScheduleId   int IDENTITY(1,1) NOT NULL CONSTRAINT PK_FeeSchedule PRIMARY KEY,
    PayerId         int           NULL CONSTRAINT FK_FeeSchedule_Payer REFERENCES dbo.Payer (PayerId),
    CptCode         varchar(5)    NOT NULL CONSTRAINT FK_FeeSchedule_Cpt REFERENCES dbo.ProcedureCode (CptCode),
    AllowedAmount   money         NOT NULL,
    EffectiveFrom   date          NOT NULL,
    EffectiveTo     date          NULL
);
GO
CREATE INDEX IX_FeeSchedule_Lookup ON dbo.FeeSchedule (CptCode, PayerId, EffectiveFrom);
GO

CREATE TABLE dbo.LabTest (
    TestCode        varchar(20)   NOT NULL CONSTRAINT PK_LabTest PRIMARY KEY,
    Name            nvarchar(100) NOT NULL,
    Specimen        varchar(20)   NULL,
    CptCode         varchar(5)    NULL,
    IsActive        bit           NOT NULL CONSTRAINT DF_LabTest_Active DEFAULT (1)
);
GO

CREATE TABLE dbo.NumberSequence (
    Name            varchar(30)   NOT NULL CONSTRAINT PK_NumberSequence PRIMARY KEY,
    NextValue       bigint        NOT NULL
);
GO

CREATE TABLE dbo.AppSetting (
    Name            varchar(100)  NOT NULL CONSTRAINT PK_AppSetting PRIMARY KEY,
    Value           nvarchar(400) NOT NULL,
    Description     nvarchar(400) NULL,
    ModifiedBy      nvarchar(256) NULL,
    ModifiedOn      datetime      NULL
);
GO

CREATE TABLE dbo.ApiClient (
    ApiClientId     int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApiClient PRIMARY KEY,
    Name            nvarchar(100) NOT NULL,
    ClientType      varchar(20)   NOT NULL, -- Kiosk, Portal, Partner
    ApiKeyHash      char(64)      NOT NULL CONSTRAINT UQ_ApiClient_Key UNIQUE,
    SiteId          int           NULL CONSTRAINT FK_ApiClient_Site REFERENCES dbo.Site (SiteId),
    IsActive        bit           NOT NULL CONSTRAINT DF_ApiClient_Active DEFAULT (1),
    LastUsedOn      datetime      NULL
);
GO

-------------------------------------------------------------------------------
-- Patients
-------------------------------------------------------------------------------
CREATE TABLE dbo.Patient (
    PatientId       int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Patient PRIMARY KEY,
    Mrn             char(9)       NOT NULL CONSTRAINT UQ_Patient_Mrn UNIQUE,
    FirstName       nvarchar(50)  NOT NULL,
    MiddleName      nvarchar(50)  NULL,
    LastName        nvarchar(50)  NOT NULL,
    DateOfBirth     date          NOT NULL,
    Sex             tinyint       NOT NULL CONSTRAINT DF_Patient_Sex DEFAULT (0),
    Address1        nvarchar(100) NULL,
    Address2        nvarchar(100) NULL,
    City            nvarchar(50)  NULL,
    State           char(2)       NULL,
    Zip             varchar(10)   NULL,
    HomePhone       varchar(20)   NULL,
    MobilePhone     varchar(20)   NULL,
    Email           nvarchar(256) NULL,
    PreferredContact varchar(10)  NOT NULL CONSTRAINT DF_Patient_Contact DEFAULT ('Phone'),
    ReminderOptIn   bit           NOT NULL CONSTRAINT DF_Patient_Reminder DEFAULT (1),
    PreferredSiteId int           NULL CONSTRAINT FK_Patient_Site REFERENCES dbo.Site (SiteId),
    PrimaryProviderId int         NULL CONSTRAINT FK_Patient_Provider REFERENCES dbo.Provider (ProviderId),
    HospitalId      varchar(20)   NULL,
    PortalUserName  nvarchar(256) NULL,
    Balance         money         NOT NULL CONSTRAINT DF_Patient_Balance DEFAULT (0),
    IsActive        bit           NOT NULL CONSTRAINT DF_Patient_Active DEFAULT (1),
    MatchKey        AS (UPPER(LastName) + '|' + CONVERT(char(8), DateOfBirth, 112)) PERSISTED,
    CreatedBy       nvarchar(256) NOT NULL,
    CreatedOn       datetime      NOT NULL CONSTRAINT DF_Patient_Created DEFAULT (GETDATE()),
    ModifiedBy      nvarchar(256) NULL,
    ModifiedOn      datetime      NULL,
    RowVersion      timestamp     NOT NULL
);
GO
CREATE INDEX IX_Patient_Name ON dbo.Patient (LastName, FirstName) INCLUDE (DateOfBirth, Mrn);
CREATE INDEX IX_Patient_MatchKey ON dbo.Patient (MatchKey);
CREATE INDEX IX_Patient_Hospital ON dbo.Patient (HospitalId) WHERE HospitalId IS NOT NULL;
CREATE UNIQUE INDEX UX_Patient_Portal ON dbo.Patient (PortalUserName) WHERE PortalUserName IS NOT NULL;
GO

CREATE TABLE dbo.PatientInsurance (
    PatientInsuranceId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PatientInsurance PRIMARY KEY,
    PatientId       int           NOT NULL CONSTRAINT FK_PatientInsurance_Patient REFERENCES dbo.Patient (PatientId),
    Sequence        tinyint       NOT NULL, -- 1 primary, 2 secondary
    PayerId         int           NOT NULL CONSTRAINT FK_PatientInsurance_Payer REFERENCES dbo.Payer (PayerId),
    MemberId        varchar(30)   NOT NULL,
    GroupNumber     varchar(30)   NULL,
    SubscriberFirstName nvarchar(50) NULL,
    SubscriberLastName  nvarchar(50) NULL,
    SubscriberDateOfBirth date    NULL,
    Relationship    tinyint       NOT NULL CONSTRAINT DF_PatientInsurance_Rel DEFAULT (18),
    Copay           money         NOT NULL CONSTRAINT DF_PatientInsurance_Copay DEFAULT (0),
    EffectiveFrom   date          NOT NULL,
    EffectiveTo     date          NULL,
    VerifiedOn      datetime      NULL
);
GO
CREATE INDEX IX_PatientInsurance_Patient ON dbo.PatientInsurance (PatientId, Sequence);
GO

CREATE TABLE dbo.PatientNote (
    PatientNoteId   int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PatientNote PRIMARY KEY,
    PatientId       int           NOT NULL CONSTRAINT FK_PatientNote_Patient REFERENCES dbo.Patient (PatientId),
    NoteType        varchar(20)   NOT NULL,
    Text            nvarchar(2000) NOT NULL,
    IsAlert         bit           NOT NULL CONSTRAINT DF_PatientNote_Alert DEFAULT (0),
    CreatedBy       nvarchar(256) NOT NULL,
    CreatedOn       datetime      NOT NULL CONSTRAINT DF_PatientNote_Created DEFAULT (GETDATE())
);
GO

CREATE TABLE dbo.IntakeForm (
    IntakeFormId    int IDENTITY(1,1) NOT NULL CONSTRAINT PK_IntakeForm PRIMARY KEY,
    PatientId       int           NOT NULL CONSTRAINT FK_IntakeForm_Patient REFERENCES dbo.Patient (PatientId),
    AppointmentId   int           NULL,
    Medications     nvarchar(2000) NULL,
    Allergies       nvarchar(1000) NULL,
    Conditions      nvarchar(2000) NULL,
    ReasonForVisit  nvarchar(1000) NULL,
    PharmacyName    nvarchar(100) NULL,
    PharmacyPhone   varchar(20)   NULL,
    SubmittedOn     datetime      NOT NULL CONSTRAINT DF_IntakeForm_Submitted DEFAULT (GETDATE()),
    ReviewedBy      nvarchar(256) NULL,
    ReviewedOn      datetime      NULL
);
GO

-------------------------------------------------------------------------------
-- Scheduling
-------------------------------------------------------------------------------
CREATE TABLE dbo.Appointment (
    AppointmentId   int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Appointment PRIMARY KEY,
    PatientId       int           NOT NULL CONSTRAINT FK_Appointment_Patient REFERENCES dbo.Patient (PatientId),
    ProviderId      int           NOT NULL CONSTRAINT FK_Appointment_Provider REFERENCES dbo.Provider (ProviderId),
    SiteId          int           NOT NULL CONSTRAINT FK_Appointment_Site REFERENCES dbo.Site (SiteId),
    AppointmentTypeId int         NOT NULL CONSTRAINT FK_Appointment_Type REFERENCES dbo.AppointmentType (AppointmentTypeId),
    StartsAt        datetime      NOT NULL,
    EndsAt          datetime      NOT NULL,
    Status          tinyint       NOT NULL CONSTRAINT DF_Appointment_Status DEFAULT (0),
    Source          tinyint       NOT NULL CONSTRAINT DF_Appointment_Source DEFAULT (0),
    Reason          nvarchar(200) NULL,
    IsOverbook      bit           NOT NULL CONSTRAINT DF_Appointment_Overbook DEFAULT (0),
    ReferralId      int           NULL,
    RescheduledFromId int         NULL CONSTRAINT FK_Appointment_From REFERENCES dbo.Appointment (AppointmentId),
    ConfirmationCode char(8)      NOT NULL,
    BookedBy        nvarchar(256) NOT NULL,
    BookedOn        datetime      NOT NULL CONSTRAINT DF_Appointment_Booked DEFAULT (GETDATE()),
    CheckedInOn     datetime      NULL,
    RoomedOn        datetime      NULL,
    CompletedOn     datetime      NULL,
    CancelledOn     datetime      NULL,
    CancelledBy     nvarchar(256) NULL,
    CancelReason    nvarchar(200) NULL,
    LateCancel      bit           NOT NULL CONSTRAINT DF_Appointment_LateCancel DEFAULT (0),
    ReminderSentOn  datetime      NULL,
    ConfirmedOn     datetime      NULL,
    CopayCollected  money         NULL,
    RowVersion      timestamp     NOT NULL,
    CONSTRAINT CK_Appointment_Times CHECK (EndsAt > StartsAt)
);
GO
CREATE INDEX IX_Appointment_Provider ON dbo.Appointment (ProviderId, StartsAt) INCLUDE (EndsAt, Status);
CREATE INDEX IX_Appointment_Site ON dbo.Appointment (SiteId, StartsAt) INCLUDE (Status, PatientId, ProviderId);
CREATE INDEX IX_Appointment_Patient ON dbo.Appointment (PatientId, StartsAt);
CREATE UNIQUE INDEX UX_Appointment_Confirmation ON dbo.Appointment (ConfirmationCode);
GO

CREATE TABLE dbo.WaitlistEntry (
    WaitlistEntryId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_WaitlistEntry PRIMARY KEY,
    PatientId       int           NOT NULL CONSTRAINT FK_Waitlist_Patient REFERENCES dbo.Patient (PatientId),
    ProviderId      int           NULL CONSTRAINT FK_Waitlist_Provider REFERENCES dbo.Provider (ProviderId),
    SiteId          int           NULL CONSTRAINT FK_Waitlist_Site REFERENCES dbo.Site (SiteId),
    AppointmentTypeId int         NOT NULL CONSTRAINT FK_Waitlist_Type REFERENCES dbo.AppointmentType (AppointmentTypeId),
    EarliestDate    date          NOT NULL,
    LatestDate      date          NULL,
    Priority        tinyint       NOT NULL CONSTRAINT DF_Waitlist_Priority DEFAULT (0),
    Notes           nvarchar(400) NULL,
    Source          varchar(20)   NOT NULL CONSTRAINT DF_Waitlist_Source DEFAULT ('Staff'),
    CreatedOn       datetime      NOT NULL CONSTRAINT DF_Waitlist_Created DEFAULT (GETDATE()),
    OfferedOn       datetime      NULL,
    ResolvedOn      datetime      NULL,
    AppointmentId   int           NULL CONSTRAINT FK_Waitlist_Appointment REFERENCES dbo.Appointment (AppointmentId)
);
GO

CREATE TABLE dbo.ReminderLog (
    ReminderLogId   int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReminderLog PRIMARY KEY,
    AppointmentId   int           NOT NULL CONSTRAINT FK_ReminderLog_Appointment REFERENCES dbo.Appointment (AppointmentId),
    Channel         varchar(10)   NOT NULL,
    Recipient       nvarchar(256) NOT NULL,
    SentOn          datetime      NOT NULL CONSTRAINT DF_ReminderLog_Sent DEFAULT (GETDATE()),
    Succeeded       bit           NOT NULL,
    Detail          nvarchar(400) NULL
);
GO

-------------------------------------------------------------------------------
-- Encounters, charges and lab
-------------------------------------------------------------------------------
CREATE TABLE dbo.Encounter (
    EncounterId     int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Encounter PRIMARY KEY,
    AppointmentId   int           NULL CONSTRAINT FK_Encounter_Appointment REFERENCES dbo.Appointment (AppointmentId),
    PatientId       int           NOT NULL CONSTRAINT FK_Encounter_Patient REFERENCES dbo.Patient (PatientId),
    ProviderId      int           NOT NULL CONSTRAINT FK_Encounter_Provider REFERENCES dbo.Provider (ProviderId),
    SiteId          int           NOT NULL CONSTRAINT FK_Encounter_Site REFERENCES dbo.Site (SiteId),
    DateOfService   date          NOT NULL,
    Status          tinyint       NOT NULL CONSTRAINT DF_Encounter_Status DEFAULT (0),
    ChiefComplaint  nvarchar(200) NULL,
    Notes           nvarchar(max) NULL,
    VitalsBp        varchar(10)   NULL,
    VitalsPulse     int           NULL,
    VitalsWeightLb  decimal(5,1)  NULL,
    VitalsTempF     decimal(4,1)  NULL,
    SignedBy        nvarchar(256) NULL,
    SignedOn        datetime      NULL,
    CreatedOn       datetime      NOT NULL CONSTRAINT DF_Encounter_Created DEFAULT (GETDATE()),
    RowVersion      timestamp     NOT NULL
);
GO
CREATE INDEX IX_Encounter_Patient ON dbo.Encounter (PatientId, DateOfService);
CREATE INDEX IX_Encounter_Status ON dbo.Encounter (Status, DateOfService);
GO

CREATE TABLE dbo.EncounterDiagnosis (
    EncounterId     int           NOT NULL CONSTRAINT FK_EncounterDiagnosis_Encounter REFERENCES dbo.Encounter (EncounterId),
    Sequence        tinyint       NOT NULL,
    Code            varchar(8)    NOT NULL,
    CodeSet         tinyint       NOT NULL,
    CONSTRAINT PK_EncounterDiagnosis PRIMARY KEY (EncounterId, Sequence)
);
GO

CREATE TABLE dbo.Charge (
    ChargeId        int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Charge PRIMARY KEY,
    EncounterId     int           NOT NULL CONSTRAINT FK_Charge_Encounter REFERENCES dbo.Encounter (EncounterId),
    LineNumber      tinyint       NOT NULL,
    CptCode         varchar(5)    NOT NULL CONSTRAINT FK_Charge_Cpt REFERENCES dbo.ProcedureCode (CptCode),
    Modifier1       char(2)       NULL,
    Modifier2       char(2)       NULL,
    Units           int           NOT NULL CONSTRAINT DF_Charge_Units DEFAULT (1),
    Amount          money         NOT NULL,
    DiagnosisPointers varchar(8)  NOT NULL CONSTRAINT DF_Charge_Pointers DEFAULT ('1'),
    Status          tinyint       NOT NULL CONSTRAINT DF_Charge_Status DEFAULT (0),
    ClaimId         int           NULL,
    PaidAmount      money         NOT NULL CONSTRAINT DF_Charge_Paid DEFAULT (0),
    AdjustmentAmount money        NOT NULL CONSTRAINT DF_Charge_Adj DEFAULT (0),
    PatientAmount   money         NOT NULL CONSTRAINT DF_Charge_Patient DEFAULT (0),
    EnteredBy       nvarchar(256) NOT NULL,
    EnteredOn       datetime      NOT NULL CONSTRAINT DF_Charge_Entered DEFAULT (GETDATE())
);
GO
CREATE INDEX IX_Charge_Encounter ON dbo.Charge (EncounterId);
CREATE INDEX IX_Charge_Claim ON dbo.Charge (ClaimId) WHERE ClaimId IS NOT NULL;
GO

CREATE TABLE dbo.LabOrder (
    LabOrderId      int IDENTITY(1,1) NOT NULL CONSTRAINT PK_LabOrder PRIMARY KEY,
    OrderNumber     varchar(20)   NOT NULL CONSTRAINT UQ_LabOrder_Number UNIQUE,
    EncounterId     int           NULL CONSTRAINT FK_LabOrder_Encounter REFERENCES dbo.Encounter (EncounterId),
    PatientId       int           NOT NULL CONSTRAINT FK_LabOrder_Patient REFERENCES dbo.Patient (PatientId),
    ProviderId      int           NOT NULL CONSTRAINT FK_LabOrder_Provider REFERENCES dbo.Provider (ProviderId),
    TestCode        varchar(20)   NOT NULL CONSTRAINT FK_LabOrder_Test REFERENCES dbo.LabTest (TestCode),
    Priority        char(1)       NOT NULL CONSTRAINT DF_LabOrder_Priority DEFAULT ('R'),
    Status          varchar(12)   NOT NULL CONSTRAINT DF_LabOrder_Status DEFAULT ('Ordered'),
    ClinicalInfo    nvarchar(200) NULL,
    OrderedOn       datetime      NOT NULL CONSTRAINT DF_LabOrder_Ordered DEFAULT (GETDATE()),
    SentOn          datetime      NULL,
    ResultedOn      datetime      NULL
);
GO

CREATE TABLE dbo.LabResult (
    LabResultId     int IDENTITY(1,1) NOT NULL CONSTRAINT PK_LabResult PRIMARY KEY,
    PatientId       int           NOT NULL CONSTRAINT FK_LabResult_Patient REFERENCES dbo.Patient (PatientId),
    LabOrderId      int           NULL CONSTRAINT FK_LabResult_Order REFERENCES dbo.LabOrder (LabOrderId),
    OrderNumber     varchar(20)   NULL,
    FillerOrderNumber varchar(30) NULL,
    TestCode        varchar(20)   NOT NULL,
    ObservationCode varchar(20)   NOT NULL,
    ObservationName nvarchar(100) NOT NULL,
    Value           nvarchar(200) NULL,
    Units           varchar(20)   NULL,
    ReferenceRange  varchar(40)   NULL,
    AbnormalFlag    varchar(4)    NULL,
    ResultStatus    char(1)       NOT NULL CONSTRAINT DF_LabResult_Status DEFAULT ('F'),
    ObservedOn      datetime      NULL,
    ReceivedOn      datetime      NOT NULL CONSTRAINT DF_LabResult_Received DEFAULT (GETDATE()),
    ReviewedBy      nvarchar(256) NULL,
    ReviewedOn      datetime      NULL,
    MessageId       int           NULL
);
GO
CREATE INDEX IX_LabResult_Patient ON dbo.LabResult (PatientId, ObservedOn);
CREATE INDEX IX_LabResult_Unreviewed ON dbo.LabResult (ReceivedOn) WHERE ReviewedOn IS NULL;
GO

-------------------------------------------------------------------------------
-- Billing
-------------------------------------------------------------------------------
CREATE TABLE dbo.ClaimBatch (
    ClaimBatchId    int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClaimBatch PRIMARY KEY,
    InterchangeControlNumber int  NOT NULL,
    FileName        varchar(100)  NOT NULL,
    ClaimCount      int           NOT NULL,
    TotalCharge     money         NOT NULL,
    Status          varchar(12)   NOT NULL CONSTRAINT DF_ClaimBatch_Status DEFAULT ('Created'),
    CreatedOn       datetime      NOT NULL CONSTRAINT DF_ClaimBatch_Created DEFAULT (GETDATE()),
    SentOn          datetime      NULL,
    AckFileName     varchar(100)  NULL,
    AckOn           datetime      NULL,
    AckStatus       char(1)       NULL
);
GO

CREATE TABLE dbo.Claim (
    ClaimId         int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Claim PRIMARY KEY,
    ClaimNumber     varchar(20)   NOT NULL CONSTRAINT UQ_Claim_Number UNIQUE,
    EncounterId     int           NOT NULL CONSTRAINT FK_Claim_Encounter REFERENCES dbo.Encounter (EncounterId),
    PatientId       int           NOT NULL CONSTRAINT FK_Claim_Patient REFERENCES dbo.Patient (PatientId),
    PayerId         int           NOT NULL CONSTRAINT FK_Claim_Payer REFERENCES dbo.Payer (PayerId),
    PatientInsuranceId int        NOT NULL CONSTRAINT FK_Claim_Insurance REFERENCES dbo.PatientInsurance (PatientInsuranceId),
    ProviderId      int           NOT NULL CONSTRAINT FK_Claim_Provider REFERENCES dbo.Provider (ProviderId),
    SiteId          int           NOT NULL CONSTRAINT FK_Claim_Site REFERENCES dbo.Site (SiteId),
    DateOfService   date          NOT NULL,
    TotalCharge     money         NOT NULL,
    Status          tinyint       NOT NULL CONSTRAINT DF_Claim_Status DEFAULT (0),
    FrequencyCode   char(1)       NOT NULL CONSTRAINT DF_Claim_Freq DEFAULT ('1'),
    OriginalClaimNumber varchar(20) NULL,
    ClaimBatchId    int           NULL CONSTRAINT FK_Claim_Batch REFERENCES dbo.ClaimBatch (ClaimBatchId),
    PayerClaimNumber varchar(30)  NULL,
    PaidAmount      money         NOT NULL CONSTRAINT DF_Claim_Paid DEFAULT (0),
    AdjustmentAmount money        NOT NULL CONSTRAINT DF_Claim_Adj DEFAULT (0),
    PatientResponsibility money   NOT NULL CONSTRAINT DF_Claim_PR DEFAULT (0),
    RejectReason    nvarchar(400) NULL,
    CreatedOn       datetime      NOT NULL CONSTRAINT DF_Claim_Created DEFAULT (GETDATE()),
    SubmittedOn     datetime      NULL,
    AdjudicatedOn   datetime      NULL
);
GO
CREATE INDEX IX_Claim_Status ON dbo.Claim (Status, CreatedOn);
CREATE INDEX IX_Claim_Patient ON dbo.Claim (PatientId);
GO
ALTER TABLE dbo.Charge ADD CONSTRAINT FK_Charge_Claim FOREIGN KEY (ClaimId) REFERENCES dbo.Claim (ClaimId);
GO

CREATE TABLE dbo.Remittance (
    RemittanceId    int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Remittance PRIMARY KEY,
    FileName        varchar(100)  NOT NULL CONSTRAINT UQ_Remittance_File UNIQUE,
    PayerId         int           NULL CONSTRAINT FK_Remittance_Payer REFERENCES dbo.Payer (PayerId),
    PayerName       nvarchar(100) NOT NULL,
    TraceNumber     varchar(30)   NOT NULL,
    PaymentMethod   char(3)       NOT NULL,
    PaymentDate     date          NOT NULL,
    TotalPaid       money         NOT NULL,
    ClaimCount      int           NOT NULL,
    UnmatchedCount  int           NOT NULL CONSTRAINT DF_Remittance_Unmatched DEFAULT (0),
    ReceivedOn      datetime      NOT NULL CONSTRAINT DF_Remittance_Received DEFAULT (GETDATE())
);
GO

CREATE TABLE dbo.Payment (
    PaymentId       int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Payment PRIMARY KEY,
    PatientId       int           NOT NULL CONSTRAINT FK_Payment_Patient REFERENCES dbo.Patient (PatientId),
    ClaimId         int           NULL CONSTRAINT FK_Payment_Claim REFERENCES dbo.Claim (ClaimId),
    RemittanceId    int           NULL CONSTRAINT FK_Payment_Remittance REFERENCES dbo.Remittance (RemittanceId),
    Source          varchar(10)   NOT NULL, -- Payer, Patient, Copay
    Method          varchar(10)   NOT NULL, -- EFT, Check, Card, Cash
    Amount          money         NOT NULL,
    AdjustmentAmount money        NOT NULL CONSTRAINT DF_Payment_Adj DEFAULT (0),
    AdjustmentReason varchar(20)  NULL,
    Reference       varchar(40)   NULL,
    PostedBy        nvarchar(256) NOT NULL,
    PostedOn        datetime      NOT NULL CONSTRAINT DF_Payment_Posted DEFAULT (GETDATE())
);
GO
CREATE INDEX IX_Payment_Patient ON dbo.Payment (PatientId, PostedOn);
GO

CREATE TABLE dbo.Statement (
    StatementId     int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Statement PRIMARY KEY,
    PatientId       int           NOT NULL CONSTRAINT FK_Statement_Patient REFERENCES dbo.Patient (PatientId),
    StatementDate   date          NOT NULL,
    PreviousBalance money         NOT NULL,
    NewCharges      money         NOT NULL,
    Payments        money         NOT NULL,
    AmountDue       money         NOT NULL,
    DueDate         date          NOT NULL,
    FileName        nvarchar(260) NULL,
    DunningLevel    tinyint       NOT NULL CONSTRAINT DF_Statement_Dunning DEFAULT (0),
    CreatedOn       datetime      NOT NULL CONSTRAINT DF_Statement_Created DEFAULT (GETDATE())
);
GO
CREATE INDEX IX_Statement_Patient ON dbo.Statement (PatientId, StatementDate);
GO

-------------------------------------------------------------------------------
-- Referrals (NHibernate module, 2013)
-------------------------------------------------------------------------------
CREATE TABLE dbo.Referral (
    ReferralId      int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Referral PRIMARY KEY,
    Version         int           NOT NULL,
    ReferralNumber  varchar(20)   NOT NULL CONSTRAINT UQ_Referral_Number UNIQUE,
    PatientId       int           NOT NULL CONSTRAINT FK_Referral_Patient REFERENCES dbo.Patient (PatientId),
    ReferringProviderName nvarchar(100) NOT NULL,
    ReferringProviderNpi  char(10) NULL,
    ReferringPractice nvarchar(100) NULL,
    ReferringPhone  varchar(20)   NULL,
    SpecialtyCode   varchar(10)   NOT NULL CONSTRAINT FK_Referral_Specialty REFERENCES dbo.Specialty (SpecialtyCode),
    RequestedProviderId int       NULL CONSTRAINT FK_Referral_Provider REFERENCES dbo.Provider (ProviderId),
    Priority        tinyint       NOT NULL,
    Reason          nvarchar(1000) NOT NULL,
    DiagnosisCode   varchar(8)    NULL,
    Status          tinyint       NOT NULL,
    Source          varchar(20)   NOT NULL,
    AuthorizationNumber varchar(30) NULL,
    VisitsAuthorized int          NULL,
    ReceivedOn      datetime      NOT NULL,
    DueBy           datetime      NOT NULL,
    DecidedBy       nvarchar(256) NULL,
    DecidedOn       datetime      NULL,
    DecisionNote    nvarchar(1000) NULL,
    AppointmentId   int           NULL CONSTRAINT FK_Referral_Appointment REFERENCES dbo.Appointment (AppointmentId),
    WorkflowInstanceId uniqueidentifier NULL,
    ExpiresOn       date          NULL
);
GO
CREATE INDEX IX_Referral_Status ON dbo.Referral (Status, DueBy);
GO
ALTER TABLE dbo.Appointment ADD CONSTRAINT FK_Appointment_Referral FOREIGN KEY (ReferralId) REFERENCES dbo.Referral (ReferralId);
GO

CREATE TABLE dbo.ReferralNote (
    ReferralNoteId  int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReferralNote PRIMARY KEY,
    ReferralId      int           NOT NULL CONSTRAINT FK_ReferralNote_Referral REFERENCES dbo.Referral (ReferralId),
    Author          nvarchar(256) NOT NULL,
    Text            nvarchar(2000) NOT NULL,
    CreatedOn       datetime      NOT NULL
);
GO

CREATE TABLE dbo.ReferralDocument (
    ReferralDocumentId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReferralDocument PRIMARY KEY,
    ReferralId      int           NOT NULL CONSTRAINT FK_ReferralDocument_Referral REFERENCES dbo.Referral (ReferralId),
    FileName        nvarchar(260) NOT NULL,
    ContentType     varchar(100)  NOT NULL,
    Content         varbinary(max) NOT NULL,
    UploadedBy      nvarchar(256) NOT NULL,
    UploadedOn      datetime      NOT NULL
);
GO

-------------------------------------------------------------------------------
-- Interfaces and audit
-------------------------------------------------------------------------------
CREATE TABLE dbo.InterfaceMessage (
    MessageId       int IDENTITY(1,1) NOT NULL CONSTRAINT PK_InterfaceMessage PRIMARY KEY,
    Direction       tinyint       NOT NULL,
    Partner         varchar(10)   NOT NULL,
    MessageType     varchar(10)   NOT NULL,
    ControlId       varchar(30)   NOT NULL,
    Status          tinyint       NOT NULL,
    PatientId       int           NULL,
    Payload         nvarchar(max) NOT NULL,
    AckPayload      nvarchar(max) NULL,
    Attempts        int           NOT NULL CONSTRAINT DF_InterfaceMessage_Attempts DEFAULT (0),
    NextAttemptOn   datetime      NULL,
    Error           nvarchar(1000) NULL,
    CreatedOn       datetime      NOT NULL CONSTRAINT DF_InterfaceMessage_Created DEFAULT (GETDATE()),
    ProcessedOn     datetime      NULL
);
GO
CREATE INDEX IX_InterfaceMessage_Outbox ON dbo.InterfaceMessage (Direction, Status, NextAttemptOn) INCLUDE (Partner);
CREATE INDEX IX_InterfaceMessage_Created ON dbo.InterfaceMessage (CreatedOn);
CREATE INDEX IX_InterfaceMessage_Control ON dbo.InterfaceMessage (ControlId);
GO

CREATE TABLE dbo.AuditLog (
    AuditLogId      bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLog PRIMARY KEY,
    UserName        nvarchar(256) NOT NULL,
    Action          varchar(40)   NOT NULL,
    EntityType      varchar(40)   NOT NULL,
    EntityId        varchar(40)   NOT NULL,
    PatientId       int           NULL,
    Details         nvarchar(1000) NULL,
    ClientAddress   varchar(45)   NULL,
    CreatedOn       datetime      NOT NULL CONSTRAINT DF_AuditLog_Created DEFAULT (GETDATE())
);
GO
CREATE INDEX IX_AuditLog_Patient ON dbo.AuditLog (PatientId, CreatedOn) WHERE PatientId IS NOT NULL;
GO

CREATE TABLE dbo.JobRun (
    JobRunId        int IDENTITY(1,1) NOT NULL CONSTRAINT PK_JobRun PRIMARY KEY,
    JobName         varchar(40)   NOT NULL,
    StartedOn       datetime      NOT NULL,
    FinishedOn      datetime      NULL,
    Succeeded       bit           NULL,
    Summary         nvarchar(1000) NULL,
    MachineName     varchar(64)   NOT NULL
);
GO
CREATE INDEX IX_JobRun_Name ON dbo.JobRun (JobName, StartedOn);
GO
