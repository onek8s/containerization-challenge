IF SCHEMA_ID('lending') IS NULL EXEC('CREATE SCHEMA lending');
GO
CREATE TABLE lending.Product (
    Code varchar(10) NOT NULL CONSTRAINT PK_Product PRIMARY KEY,
    Name nvarchar(100) NOT NULL,
    CoreProductCode char(4) NOT NULL,
    MinAmount decimal(13,2) NOT NULL,
    MaxAmount decimal(13,2) NOT NULL,
    MinTermMonths int NOT NULL,
    MaxTermMonths int NOT NULL,
    ReferenceRate int NOT NULL,
    BaseMarginPercent decimal(7,4) NOT NULL,
    ArrangementFee decimal(9,2) NOT NULL,
    MonthlyFee decimal(7,2) NOT NULL,
    RequiresCollateral bit NOT NULL,
    AllowsCoApplicant bit NOT NULL,
    Purposes varchar(200) NOT NULL,
    Active bit NOT NULL
);
CREATE TABLE lending.Sequence (
    Name varchar(30) NOT NULL CONSTRAINT PK_Sequence PRIMARY KEY,
    Value bigint NOT NULL
);
CREATE TABLE lending.Application (
    Id bigint IDENTITY CONSTRAINT PK_Application PRIMARY KEY,
    Reference varchar(20) NOT NULL CONSTRAINT UQ_Application_Reference UNIQUE,
    ProductCode varchar(10) NOT NULL CONSTRAINT FK_Application_Product REFERENCES lending.Product(Code),
    Purpose int NOT NULL,
    Amount decimal(13,2) NOT NULL,
    TermMonths int NOT NULL,
    DueDay int NOT NULL,
    Channel int NOT NULL,
    Status int NOT NULL,
    CollateralDescription nvarchar(200) NULL,
    CollateralValue decimal(13,2) NULL,
    DisbursementIban varchar(34) NULL,
    ConsentBureau bit NOT NULL,
    ConsentMarketing bit NOT NULL,
    CustomerUserId nvarchar(450) NULL,
    AssignedTo nvarchar(100) NULL,
    CoreAccountNumber varchar(12) NULL,
    Attempts int NOT NULL CONSTRAINT DF_Application_Attempts DEFAULT 0,
    LastError nvarchar(1000) NULL,
    CreatedUtc datetime2(3) NOT NULL,
    SubmittedUtc datetime2(3) NULL,
    DecidedUtc datetime2(3) NULL,
    UpdatedUtc datetime2(3) NOT NULL,
    Version int NOT NULL CONSTRAINT DF_Application_Version DEFAULT 1
);
CREATE INDEX IX_Application_Status ON lending.Application(Status, UpdatedUtc);
CREATE INDEX IX_Application_Customer ON lending.Application(CustomerUserId);
CREATE TABLE lending.Applicant (
    Id bigint IDENTITY CONSTRAINT PK_Applicant PRIMARY KEY,
    ApplicationId bigint NOT NULL CONSTRAINT FK_Applicant_Application REFERENCES lending.Application(Id),
    IsMain bit NOT NULL,
    PersonalIdentityCode char(11) NOT NULL,
    FirstName nvarchar(50) NOT NULL,
    LastName nvarchar(50) NOT NULL,
    Email nvarchar(200) NOT NULL,
    Phone varchar(30) NOT NULL,
    StreetAddress nvarchar(100) NOT NULL,
    PostalCode varchar(10) NOT NULL,
    City nvarchar(50) NOT NULL,
    Citizenship char(2) NOT NULL,
    ResidentInFinland bit NOT NULL,
    MaritalStatus int NOT NULL,
    Housing int NOT NULL,
    Adults int NOT NULL,
    Children int NOT NULL,
    Employment int NOT NULL,
    Employer nvarchar(100) NOT NULL,
    EmployedSince date NULL,
    OnProbation bit NOT NULL,
    GrossMonthlyIncome decimal(11,2) NOT NULL,
    NetMonthlyIncome decimal(11,2) NOT NULL,
    HousingCostMonthly decimal(11,2) NOT NULL,
    OtherLoanPaymentsMonthly decimal(11,2) NOT NULL,
    OtherLoanBalance decimal(13,2) NOT NULL,
    PoliticallyExposed bit NOT NULL,
    CoreCustomerNumber char(10) NULL
);
CREATE INDEX IX_Applicant_Pic ON lending.Applicant(PersonalIdentityCode);
CREATE TABLE lending.BureauReport (
    Id bigint IDENTITY CONSTRAINT PK_BureauReport PRIMARY KEY,
    ApplicantId bigint NOT NULL CONSTRAINT FK_BureauReport_Applicant REFERENCES lending.Applicant(Id),
    Provider varchar(20) NOT NULL,
    RequestId varchar(50) NOT NULL,
    Score int NULL,
    PaymentDefault bit NOT NULL,
    DefaultCount int NOT NULL,
    DebtRestructuring bit NOT NULL,
    CreditBlock bit NOT NULL,
    ReportedLoans int NOT NULL,
    RawXml nvarchar(max) NOT NULL,
    CreatedUtc datetime2(3) NOT NULL
);
CREATE TABLE lending.Decision (
    Id bigint IDENTITY CONSTRAINT PK_Decision PRIMARY KEY,
    ApplicationId bigint NOT NULL CONSTRAINT FK_Decision_Application REFERENCES lending.Application(Id),
    Outcome varchar(10) NOT NULL,
    RiskGrade char(1) NOT NULL,
    Score int NOT NULL,
    ReasonCodes varchar(400) NOT NULL,
    ScorecardJson nvarchar(max) NOT NULL,
    RuleTraceJson nvarchar(max) NOT NULL,
    AffordabilityJson nvarchar(max) NOT NULL,
    PolicyVersion varchar(20) NOT NULL,
    DecidedBy nvarchar(100) NOT NULL,
    IsOverride bit NOT NULL,
    Comment nvarchar(1000) NULL,
    CreatedUtc datetime2(3) NOT NULL
);
CREATE TABLE lending.Offer (
    Id bigint IDENTITY CONSTRAINT PK_Offer PRIMARY KEY,
    ApplicationId bigint NOT NULL CONSTRAINT FK_Offer_Application REFERENCES lending.Application(Id),
    Amount decimal(13,2) NOT NULL,
    TermMonths int NOT NULL,
    ReferenceRateCode char(3) NOT NULL,
    ReferenceRatePercent decimal(7,4) NOT NULL,
    MarginPercent decimal(7,4) NOT NULL,
    NominalRatePercent decimal(7,4) NOT NULL,
    AprPercent decimal(7,2) NOT NULL,
    MonthlyPayment decimal(11,2) NOT NULL,
    MonthlyFee decimal(7,2) NOT NULL,
    ArrangementFee decimal(9,2) NOT NULL,
    TotalRepayable decimal(13,2) NOT NULL,
    ValidUntilUtc datetime2(3) NOT NULL,
    Status varchar(10) NOT NULL,
    CreatedUtc datetime2(3) NOT NULL,
    AcceptedUtc datetime2(3) NULL
);
CREATE TABLE lending.Document (
    Id bigint IDENTITY CONSTRAINT PK_Document PRIMARY KEY,
    ApplicationId bigint NULL CONSTRAINT FK_Document_Application REFERENCES lending.Application(Id),
    CollectionCaseId bigint NULL,
    Kind varchar(30) NOT NULL,
    FileName nvarchar(200) NOT NULL,
    ContentType varchar(100) NOT NULL,
    Content varbinary(max) NULL,
    Sha256 char(64) NULL,
    Status varchar(20) NOT NULL,
    SigningOrderId varchar(60) NULL,
    SigningUrl nvarchar(400) NULL,
    CreatedUtc datetime2(3) NOT NULL,
    SignedUtc datetime2(3) NULL
);
CREATE INDEX IX_Document_Application ON lending.Document(ApplicationId);
CREATE INDEX IX_Document_SigningOrder ON lending.Document(SigningOrderId);
CREATE TABLE lending.Note (
    Id bigint IDENTITY CONSTRAINT PK_Note PRIMARY KEY,
    ApplicationId bigint NOT NULL CONSTRAINT FK_Note_Application REFERENCES lending.Application(Id),
    Author nvarchar(100) NOT NULL,
    Text nvarchar(2000) NOT NULL,
    CreatedUtc datetime2(3) NOT NULL
);
CREATE TABLE lending.AuditEvent (
    Id bigint IDENTITY CONSTRAINT PK_AuditEvent PRIMARY KEY,
    ApplicationId bigint NULL,
    EventType varchar(40) NOT NULL,
    Actor nvarchar(100) NOT NULL,
    Detail nvarchar(2000) NULL,
    CreatedUtc datetime2(3) NOT NULL
);
CREATE INDEX IX_AuditEvent_Application ON lending.AuditEvent(ApplicationId, CreatedUtc);
CREATE TABLE lending.Outbox (
    Id bigint IDENTITY CONSTRAINT PK_Outbox PRIMARY KEY,
    Topic varchar(50) NOT NULL,
    Payload nvarchar(max) NOT NULL,
    CreatedUtc datetime2(3) NOT NULL,
    NextAttemptUtc datetime2(3) NOT NULL,
    Attempts int NOT NULL,
    LockedBy varchar(100) NULL,
    LockedUntilUtc datetime2(3) NULL,
    ProcessedUtc datetime2(3) NULL,
    LastError nvarchar(2000) NULL
);
CREATE INDEX IX_Outbox_Pending ON lending.Outbox(Topic, ProcessedUtc, NextAttemptUtc);
CREATE TABLE lending.Loan (
    AccountNumber varchar(12) NOT NULL CONSTRAINT PK_Loan PRIMARY KEY,
    ApplicationId bigint NULL CONSTRAINT FK_Loan_Application REFERENCES lending.Application(Id),
    CustomerNumber char(10) NOT NULL,
    ProductCode varchar(10) NOT NULL,
    Amount decimal(13,2) NOT NULL,
    NominalRatePercent decimal(7,4) NOT NULL,
    MonthlyInstalment decimal(11,2) NOT NULL,
    BookedUtc datetime2(3) NOT NULL,
    DisbursedUtc datetime2(3) NULL,
    Balance decimal(13,2) NOT NULL,
    Arrears decimal(13,2) NOT NULL,
    DaysPastDue int NOT NULL,
    CoreStatus varchar(4) NOT NULL,
    LastEventUtc datetime2(3) NULL
);
CREATE TABLE lending.CollectionCase (
    Id bigint IDENTITY CONSTRAINT PK_CollectionCase PRIMARY KEY,
    Reference varchar(20) NOT NULL CONSTRAINT UQ_CollectionCase_Reference UNIQUE,
    AccountNumber varchar(12) NOT NULL,
    CustomerNumber char(10) NOT NULL,
    Stage int NOT NULL,
    Status int NOT NULL,
    Arrears decimal(13,2) NOT NULL,
    DaysPastDue int NOT NULL,
    FeesCharged int NOT NULL,
    AssignedTo nvarchar(100) NULL,
    PromiseDate date NULL,
    PromiseAmount decimal(13,2) NULL,
    OpenedUtc datetime2(3) NOT NULL,
    LastActionUtc datetime2(3) NULL,
    ClosedUtc datetime2(3) NULL,
    Version int NOT NULL CONSTRAINT DF_CollectionCase_Version DEFAULT 1
);
CREATE INDEX IX_CollectionCase_Account ON lending.CollectionCase(AccountNumber, Status);
CREATE TABLE lending.CollectionAction (
    Id bigint IDENTITY CONSTRAINT PK_CollectionAction PRIMARY KEY,
    CaseId bigint NOT NULL CONSTRAINT FK_CollectionAction_Case REFERENCES lending.CollectionCase(Id),
    ActionType varchar(30) NOT NULL,
    Detail nvarchar(1000) NULL,
    Actor nvarchar(100) NOT NULL,
    CreatedUtc datetime2(3) NOT NULL
);
CREATE TABLE lending.PaymentPlan (
    Id bigint IDENTITY CONSTRAINT PK_PaymentPlan PRIMARY KEY,
    CaseId bigint NOT NULL CONSTRAINT FK_PaymentPlan_Case REFERENCES lending.CollectionCase(Id),
    AccountNumber varchar(12) NOT NULL,
    Months int NOT NULL,
    InstalmentsJson nvarchar(max) NOT NULL,
    Status varchar(10) NOT NULL,
    CreatedBy nvarchar(100) NOT NULL,
    CreatedUtc datetime2(3) NOT NULL
);
CREATE TABLE lending.GlEntry (
    Id bigint IDENTITY CONSTRAINT PK_GlEntry PRIMARY KEY,
    BusinessDate date NOT NULL,
    GlAccount varchar(10) NOT NULL,
    CostCenter varchar(10) NOT NULL,
    Debit decimal(15,2) NOT NULL,
    Credit decimal(15,2) NOT NULL,
    Text nvarchar(100) NOT NULL,
    SourceType varchar(20) NOT NULL,
    SourceRef varchar(40) NOT NULL,
    ExportBatch varchar(30) NULL,
    CreatedUtc datetime2(3) NOT NULL
);
CREATE INDEX IX_GlEntry_Export ON lending.GlEntry(ExportBatch, BusinessDate);
CREATE TABLE lending.JobRun (
    Id bigint IDENTITY CONSTRAINT PK_JobRun PRIMARY KEY,
    Job varchar(50) NOT NULL,
    Host nvarchar(100) NOT NULL,
    StartedUtc datetime2(3) NOT NULL,
    FinishedUtc datetime2(3) NULL,
    Status varchar(10) NOT NULL,
    Detail nvarchar(2000) NULL
);
CREATE TABLE lending.ApiClient (
    Id int IDENTITY CONSTRAINT PK_ApiClient PRIMARY KEY,
    Name varchar(50) NOT NULL CONSTRAINT UQ_ApiClient_Name UNIQUE,
    KeyHash char(64) NOT NULL,
    Scopes varchar(200) NOT NULL,
    Active bit NOT NULL
);
