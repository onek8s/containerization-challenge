-- SQLite schema for development ; keep in step with SqlServer/0001-schema.sql
CREATE TABLE Product (
    Code TEXT NOT NULL PRIMARY KEY,
    Name TEXT NOT NULL,
    CoreProductCode TEXT NOT NULL,
    MinAmount NUMERIC NOT NULL,
    MaxAmount NUMERIC NOT NULL,
    MinTermMonths INTEGER NOT NULL,
    MaxTermMonths INTEGER NOT NULL,
    ReferenceRate INTEGER NOT NULL,
    BaseMarginPercent NUMERIC NOT NULL,
    ArrangementFee NUMERIC NOT NULL,
    MonthlyFee NUMERIC NOT NULL,
    RequiresCollateral INTEGER NOT NULL,
    AllowsCoApplicant INTEGER NOT NULL,
    Purposes TEXT NOT NULL,
    Active INTEGER NOT NULL
);
CREATE TABLE Sequence (
    Name TEXT NOT NULL PRIMARY KEY,
    Value INTEGER NOT NULL
);
CREATE TABLE Application (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Reference TEXT NOT NULL UNIQUE,
    ProductCode TEXT NOT NULL REFERENCES Product(Code),
    Purpose INTEGER NOT NULL,
    Amount NUMERIC NOT NULL,
    TermMonths INTEGER NOT NULL,
    DueDay INTEGER NOT NULL,
    Channel INTEGER NOT NULL,
    Status INTEGER NOT NULL,
    CollateralDescription TEXT NULL,
    CollateralValue NUMERIC NULL,
    DisbursementIban TEXT NULL,
    ConsentBureau INTEGER NOT NULL,
    ConsentMarketing INTEGER NOT NULL,
    CustomerUserId TEXT NULL,
    AssignedTo TEXT NULL,
    CoreAccountNumber TEXT NULL,
    Attempts INTEGER NOT NULL DEFAULT 0,
    LastError TEXT NULL,
    CreatedUtc TEXT NOT NULL,
    SubmittedUtc TEXT NULL,
    DecidedUtc TEXT NULL,
    UpdatedUtc TEXT NOT NULL,
    Version INTEGER NOT NULL DEFAULT 1
);
CREATE INDEX IX_Application_Status ON Application(Status, UpdatedUtc);
CREATE INDEX IX_Application_Customer ON Application(CustomerUserId);
CREATE TABLE Applicant (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ApplicationId INTEGER NOT NULL REFERENCES Application(Id),
    IsMain INTEGER NOT NULL,
    PersonalIdentityCode TEXT NOT NULL,
    FirstName TEXT NOT NULL,
    LastName TEXT NOT NULL,
    Email TEXT NOT NULL,
    Phone TEXT NOT NULL,
    StreetAddress TEXT NOT NULL,
    PostalCode TEXT NOT NULL,
    City TEXT NOT NULL,
    Citizenship TEXT NOT NULL,
    ResidentInFinland INTEGER NOT NULL,
    MaritalStatus INTEGER NOT NULL,
    Housing INTEGER NOT NULL,
    Adults INTEGER NOT NULL,
    Children INTEGER NOT NULL,
    Employment INTEGER NOT NULL,
    Employer TEXT NOT NULL,
    EmployedSince TEXT NULL,
    OnProbation INTEGER NOT NULL,
    GrossMonthlyIncome NUMERIC NOT NULL,
    NetMonthlyIncome NUMERIC NOT NULL,
    HousingCostMonthly NUMERIC NOT NULL,
    OtherLoanPaymentsMonthly NUMERIC NOT NULL,
    OtherLoanBalance NUMERIC NOT NULL,
    PoliticallyExposed INTEGER NOT NULL,
    CoreCustomerNumber TEXT NULL
);
CREATE INDEX IX_Applicant_Pic ON Applicant(PersonalIdentityCode);
CREATE TABLE BureauReport (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ApplicantId INTEGER NOT NULL REFERENCES Applicant(Id),
    Provider TEXT NOT NULL,
    RequestId TEXT NOT NULL,
    Score INTEGER NULL,
    PaymentDefault INTEGER NOT NULL,
    DefaultCount INTEGER NOT NULL,
    DebtRestructuring INTEGER NOT NULL,
    CreditBlock INTEGER NOT NULL,
    ReportedLoans INTEGER NOT NULL,
    RawXml TEXT NOT NULL,
    CreatedUtc TEXT NOT NULL
);
CREATE TABLE Decision (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ApplicationId INTEGER NOT NULL REFERENCES Application(Id),
    Outcome TEXT NOT NULL,
    RiskGrade TEXT NOT NULL,
    Score INTEGER NOT NULL,
    ReasonCodes TEXT NOT NULL,
    ScorecardJson TEXT NOT NULL,
    RuleTraceJson TEXT NOT NULL,
    AffordabilityJson TEXT NOT NULL,
    PolicyVersion TEXT NOT NULL,
    DecidedBy TEXT NOT NULL,
    IsOverride INTEGER NOT NULL,
    Comment TEXT NULL,
    CreatedUtc TEXT NOT NULL
);
CREATE TABLE Offer (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ApplicationId INTEGER NOT NULL REFERENCES Application(Id),
    Amount NUMERIC NOT NULL,
    TermMonths INTEGER NOT NULL,
    ReferenceRateCode TEXT NOT NULL,
    ReferenceRatePercent NUMERIC NOT NULL,
    MarginPercent NUMERIC NOT NULL,
    NominalRatePercent NUMERIC NOT NULL,
    AprPercent NUMERIC NOT NULL,
    MonthlyPayment NUMERIC NOT NULL,
    MonthlyFee NUMERIC NOT NULL,
    ArrangementFee NUMERIC NOT NULL,
    TotalRepayable NUMERIC NOT NULL,
    ValidUntilUtc TEXT NOT NULL,
    Status TEXT NOT NULL,
    CreatedUtc TEXT NOT NULL,
    AcceptedUtc TEXT NULL
);
CREATE TABLE Document (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ApplicationId INTEGER NULL REFERENCES Application(Id),
    CollectionCaseId INTEGER NULL,
    Kind TEXT NOT NULL,
    FileName TEXT NOT NULL,
    ContentType TEXT NOT NULL,
    Content BLOB NULL,
    Sha256 TEXT NULL,
    Status TEXT NOT NULL,
    SigningOrderId TEXT NULL,
    SigningUrl TEXT NULL,
    CreatedUtc TEXT NOT NULL,
    SignedUtc TEXT NULL
);
CREATE INDEX IX_Document_Application ON Document(ApplicationId);
CREATE INDEX IX_Document_SigningOrder ON Document(SigningOrderId);
CREATE TABLE Note (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ApplicationId INTEGER NOT NULL REFERENCES Application(Id),
    Author TEXT NOT NULL,
    Text TEXT NOT NULL,
    CreatedUtc TEXT NOT NULL
);
CREATE TABLE AuditEvent (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ApplicationId INTEGER NULL,
    EventType TEXT NOT NULL,
    Actor TEXT NOT NULL,
    Detail TEXT NULL,
    CreatedUtc TEXT NOT NULL
);
CREATE INDEX IX_AuditEvent_Application ON AuditEvent(ApplicationId, CreatedUtc);
CREATE TABLE Outbox (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Topic TEXT NOT NULL,
    Payload TEXT NOT NULL,
    CreatedUtc TEXT NOT NULL,
    NextAttemptUtc TEXT NOT NULL,
    Attempts INTEGER NOT NULL,
    LockedBy TEXT NULL,
    LockedUntilUtc TEXT NULL,
    ProcessedUtc TEXT NULL,
    LastError TEXT NULL
);
CREATE INDEX IX_Outbox_Pending ON Outbox(Topic, ProcessedUtc, NextAttemptUtc);
CREATE TABLE Loan (
    AccountNumber TEXT NOT NULL PRIMARY KEY,
    ApplicationId INTEGER NULL REFERENCES Application(Id),
    CustomerNumber TEXT NOT NULL,
    ProductCode TEXT NOT NULL,
    Amount NUMERIC NOT NULL,
    NominalRatePercent NUMERIC NOT NULL,
    MonthlyInstalment NUMERIC NOT NULL,
    BookedUtc TEXT NOT NULL,
    DisbursedUtc TEXT NULL,
    Balance NUMERIC NOT NULL,
    Arrears NUMERIC NOT NULL,
    DaysPastDue INTEGER NOT NULL,
    CoreStatus TEXT NOT NULL,
    LastEventUtc TEXT NULL
);
CREATE TABLE CollectionCase (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Reference TEXT NOT NULL UNIQUE,
    AccountNumber TEXT NOT NULL,
    CustomerNumber TEXT NOT NULL,
    Stage INTEGER NOT NULL,
    Status INTEGER NOT NULL,
    Arrears NUMERIC NOT NULL,
    DaysPastDue INTEGER NOT NULL,
    FeesCharged INTEGER NOT NULL,
    AssignedTo TEXT NULL,
    PromiseDate TEXT NULL,
    PromiseAmount NUMERIC NULL,
    OpenedUtc TEXT NOT NULL,
    LastActionUtc TEXT NULL,
    ClosedUtc TEXT NULL,
    Version INTEGER NOT NULL DEFAULT 1
);
CREATE INDEX IX_CollectionCase_Account ON CollectionCase(AccountNumber, Status);
CREATE TABLE CollectionAction (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CaseId INTEGER NOT NULL REFERENCES CollectionCase(Id),
    ActionType TEXT NOT NULL,
    Detail TEXT NULL,
    Actor TEXT NOT NULL,
    CreatedUtc TEXT NOT NULL
);
CREATE TABLE PaymentPlan (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CaseId INTEGER NOT NULL REFERENCES CollectionCase(Id),
    AccountNumber TEXT NOT NULL,
    Months INTEGER NOT NULL,
    InstalmentsJson TEXT NOT NULL,
    Status TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    CreatedUtc TEXT NOT NULL
);
CREATE TABLE GlEntry (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    BusinessDate TEXT NOT NULL,
    GlAccount TEXT NOT NULL,
    CostCenter TEXT NOT NULL,
    Debit NUMERIC NOT NULL,
    Credit NUMERIC NOT NULL,
    Text TEXT NOT NULL,
    SourceType TEXT NOT NULL,
    SourceRef TEXT NOT NULL,
    ExportBatch TEXT NULL,
    CreatedUtc TEXT NOT NULL
);
CREATE INDEX IX_GlEntry_Export ON GlEntry(ExportBatch, BusinessDate);
CREATE TABLE JobRun (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Job TEXT NOT NULL,
    Host TEXT NOT NULL,
    StartedUtc TEXT NOT NULL,
    FinishedUtc TEXT NULL,
    Status TEXT NOT NULL,
    Detail TEXT NULL
);
CREATE TABLE ApiClient (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    KeyHash TEXT NOT NULL,
    Scopes TEXT NOT NULL,
    Active INTEGER NOT NULL
);
