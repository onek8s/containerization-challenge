-- Account Statements schema. sqlcmd -S sql01.corp.local -E -d AccountStatements -i 02-schema.sql
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF SCHEMA_ID(N'stm') IS NULL EXEC(N'CREATE SCHEMA stm');
GO

-- Company details printed on every document.
IF OBJECT_ID(N'stm.CompanyProfile') IS NULL
CREATE TABLE stm.CompanyProfile
(
    ProfileId       int            NOT NULL CONSTRAINT PK_CompanyProfile PRIMARY KEY,
    CompanyName     nvarchar(100)  NOT NULL,
    AddressLine1    nvarchar(100)  NOT NULL,
    PostalCode      nvarchar(20)   NOT NULL,
    City            nvarchar(60)   NOT NULL,
    BusinessId      nvarchar(20)   NOT NULL,
    VatNumber       nvarchar(20)   NOT NULL,
    Phone           nvarchar(30)   NOT NULL,
    Email           nvarchar(120)  NOT NULL,
    Web             nvarchar(120)  NOT NULL,
    BankName        nvarchar(60)   NOT NULL,
    Iban            nvarchar(34)   NOT NULL,
    Bic             nvarchar(11)   NOT NULL,
    Logo            varbinary(max) NULL
);
GO

IF OBJECT_ID(N'stm.Customer') IS NULL
CREATE TABLE stm.Customer
(
    CustomerId       int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customer PRIMARY KEY,
    CustomerNo       varchar(12)    NOT NULL CONSTRAINT UQ_Customer_No UNIQUE,
    Name             nvarchar(120)  NOT NULL,
    AddressLine1     nvarchar(100)  NULL,
    AddressLine2     nvarchar(100)  NULL,
    PostalCode       nvarchar(20)   NULL,
    City             nvarchar(60)   NULL,
    CountryCode      char(2)        NOT NULL CONSTRAINT DF_Customer_Country DEFAULT ('FI'),
    Email            nvarchar(200)  NULL,
    Language         varchar(5)     NOT NULL CONSTRAINT DF_Customer_Language DEFAULT ('fi-FI'),
    CurrencyCode     char(3)        NOT NULL CONSTRAINT DF_Customer_Currency DEFAULT ('EUR'),
    -- 1 = e-mail, 2 = customer drop folder on the statement share, 3 = printed and posted, 9 = no statement
    Delivery         tinyint        NOT NULL CONSTRAINT DF_Customer_Delivery DEFAULT (1),
    DropFolder       nvarchar(260)  NULL,
    CreditController nvarchar(60)   NULL,
    CreditLimit      decimal(18,2)  NOT NULL CONSTRAINT DF_Customer_CreditLimit DEFAULT (0),
    OnHold           bit            NOT NULL CONSTRAINT DF_Customer_OnHold DEFAULT (0),
    ModifiedAt       datetime2(0)   NOT NULL CONSTRAINT DF_Customer_Modified DEFAULT (SYSDATETIME()),
    CONSTRAINT CK_Customer_Delivery CHECK (Delivery IN (1, 2, 3, 9))
);
GO

IF OBJECT_ID(N'stm.LeaseContract') IS NULL
CREATE TABLE stm.LeaseContract
(
    ContractId       int IDENTITY(1,1) NOT NULL CONSTRAINT PK_LeaseContract PRIMARY KEY,
    ContractNo       varchar(16)    NOT NULL CONSTRAINT UQ_LeaseContract_No UNIQUE,
    CustomerId       int            NOT NULL CONSTRAINT FK_LeaseContract_Customer REFERENCES stm.Customer (CustomerId),
    AssetCategory    nvarchar(40)   NOT NULL,
    AssetDescription nvarchar(200)  NOT NULL,
    StartDate        date           NOT NULL,
    EndDate          date           NOT NULL,
    MonthlyRent      decimal(18,2)  NOT NULL,
    AssetValue       decimal(18,2)  NOT NULL,
    ResidualValue    decimal(18,2)  NOT NULL CONSTRAINT DF_LeaseContract_Residual DEFAULT (0),
    Status           varchar(12)    NOT NULL CONSTRAINT DF_LeaseContract_Status DEFAULT ('ACTIVE'),
    CONSTRAINT CK_LeaseContract_Status CHECK (Status IN ('ACTIVE', 'ENDED', 'TERMINATED', 'PENDING'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LeaseContract_Customer')
    CREATE INDEX IX_LeaseContract_Customer ON stm.LeaseContract (CustomerId);
GO

-- Accounts receivable ledger, loaded nightly from the ERP (stm.LedgerLoad logs the loads).
IF OBJECT_ID(N'stm.LedgerEntry') IS NULL
CREATE TABLE stm.LedgerEntry
(
    EntryId          bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_LedgerEntry PRIMARY KEY,
    CustomerId       int            NOT NULL CONSTRAINT FK_LedgerEntry_Customer REFERENCES stm.Customer (CustomerId),
    EntryType        char(3)        NOT NULL,
    DocumentNo       varchar(20)    NOT NULL,
    ContractNo       varchar(16)    NULL,
    PostingDate      date           NOT NULL,
    DueDate          date           NULL,
    Description      nvarchar(200)  NULL,
    Amount           decimal(18,2)  NOT NULL,
    OpenAmount       decimal(18,2)  NOT NULL,
    ClosedDate       date           NULL,
    PaymentReference varchar(25)    NULL,
    CONSTRAINT CK_LedgerEntry_Type CHECK (EntryType IN ('INV', 'CRN', 'PAY', 'ADJ', 'INT')),
    CONSTRAINT UQ_LedgerEntry_Document UNIQUE (EntryType, DocumentNo)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LedgerEntry_Customer')
    CREATE INDEX IX_LedgerEntry_Customer ON stm.LedgerEntry (CustomerId, PostingDate) INCLUDE (EntryType, Amount, OpenAmount, ClosedDate, DueDate);
GO

IF OBJECT_ID(N'stm.LedgerLoad') IS NULL
CREATE TABLE stm.LedgerLoad
(
    LoadId           int IDENTITY(1,1) NOT NULL CONSTRAINT PK_LedgerLoad PRIMARY KEY,
    LoadedAt         datetime2(0)   NOT NULL,
    LedgerDate       date           NOT NULL,
    Entries          int            NOT NULL,
    Source           nvarchar(60)   NOT NULL
);
GO

IF OBJECT_ID(N'stm.Invoice') IS NULL
CREATE TABLE stm.Invoice
(
    InvoiceId        int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Invoice PRIMARY KEY,
    InvoiceNo        varchar(20)    NOT NULL CONSTRAINT UQ_Invoice_No UNIQUE,
    CustomerId       int            NOT NULL CONSTRAINT FK_Invoice_Customer REFERENCES stm.Customer (CustomerId),
    ContractNo       varchar(16)    NULL,
    InvoiceDate      date           NOT NULL,
    DueDate          date           NOT NULL,
    PeriodFrom       date           NULL,
    PeriodTo         date           NULL,
    NetAmount        decimal(18,2)  NOT NULL,
    VatAmount        decimal(18,2)  NOT NULL,
    TotalAmount      decimal(18,2)  NOT NULL,
    PaymentReference varchar(25)    NOT NULL,
    CurrencyCode     char(3)        NOT NULL CONSTRAINT DF_Invoice_Currency DEFAULT ('EUR'),
    CustomerReference nvarchar(60)  NULL
);
GO

IF OBJECT_ID(N'stm.InvoiceLine') IS NULL
CREATE TABLE stm.InvoiceLine
(
    InvoiceId        int            NOT NULL CONSTRAINT FK_InvoiceLine_Invoice REFERENCES stm.Invoice (InvoiceId),
    LineNumber       smallint       NOT NULL,
    Description      nvarchar(200)  NOT NULL,
    Quantity         decimal(9,2)   NOT NULL,
    UnitPrice        decimal(18,2)  NOT NULL,
    VatRate          decimal(5,2)   NOT NULL,
    NetAmount        decimal(18,2)  NOT NULL,
    VatAmount        decimal(18,2)  NOT NULL,
    CONSTRAINT PK_InvoiceLine PRIMARY KEY (InvoiceId, LineNumber)
);
GO

IF OBJECT_ID(N'stm.Holiday') IS NULL
CREATE TABLE stm.Holiday
(
    HolidayDate      date           NOT NULL CONSTRAINT PK_Holiday PRIMARY KEY,
    Name             nvarchar(60)   NOT NULL
);
GO

IF OBJECT_ID(N'stm.Subscription') IS NULL
CREATE TABLE stm.Subscription
(
    SubscriptionId   int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Subscription PRIMARY KEY,
    Name             nvarchar(100)  NOT NULL,
    ReportCode       varchar(20)    NOT NULL,
    Schedule         varchar(40)    NOT NULL,
    Parameters       nvarchar(1000) NULL,
    OutputFormat     varchar(10)    NOT NULL CONSTRAINT DF_Subscription_Format DEFAULT ('PDF'),
    -- EMAIL, SHARE or CUSTOMER (statement run: each customer's own delivery setting)
    DeliveryMethod   varchar(10)    NOT NULL,
    Recipients       nvarchar(1000) NULL,
    SharePath        nvarchar(260)  NULL,
    FileNameTemplate nvarchar(120)  NULL,
    EmailSubject     nvarchar(200)  NULL,
    Owner            nvarchar(100)  NOT NULL,
    Enabled          bit            NOT NULL CONSTRAINT DF_Subscription_Enabled DEFAULT (1),
    NextRunAt        datetime2(0)   NULL,
    LastRunAt        datetime2(0)   NULL,
    LastStatus       varchar(20)    NULL,
    ClaimedBy        nvarchar(60)   NULL,
    ClaimedUntil     datetime2(0)   NULL,
    ModifiedBy       nvarchar(100)  NOT NULL,
    ModifiedAt       datetime2(0)   NOT NULL CONSTRAINT DF_Subscription_Modified DEFAULT (SYSDATETIME()),
    CONSTRAINT CK_Subscription_Delivery CHECK (DeliveryMethod IN ('EMAIL', 'SHARE', 'CUSTOMER'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Subscription_Due')
    CREATE INDEX IX_Subscription_Due ON stm.Subscription (NextRunAt) WHERE Enabled = 1;
GO

IF OBJECT_ID(N'stm.SubscriptionRun') IS NULL
CREATE TABLE stm.SubscriptionRun
(
    RunId            int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SubscriptionRun PRIMARY KEY,
    SubscriptionId   int            NOT NULL CONSTRAINT FK_SubscriptionRun_Subscription REFERENCES stm.Subscription (SubscriptionId),
    ScheduledFor     datetime2(0)   NULL,
    StartedAt        datetime2(0)   NOT NULL,
    FinishedAt       datetime2(0)   NULL,
    Status           varchar(20)    NOT NULL,
    Documents        int            NOT NULL CONSTRAINT DF_SubscriptionRun_Documents DEFAULT (0),
    Failures         int            NOT NULL CONSTRAINT DF_SubscriptionRun_Failures DEFAULT (0),
    Message          nvarchar(2000) NULL,
    RunBy            nvarchar(100)  NOT NULL,
    Host             nvarchar(60)   NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubscriptionRun_Subscription')
    CREATE INDEX IX_SubscriptionRun_Subscription ON stm.SubscriptionRun (SubscriptionId, StartedAt DESC);
GO

IF OBJECT_ID(N'stm.Delivery') IS NULL
CREATE TABLE stm.Delivery
(
    DeliveryId       bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Delivery PRIMARY KEY,
    RunId            int            NOT NULL CONSTRAINT FK_Delivery_Run REFERENCES stm.SubscriptionRun (RunId),
    CustomerId       int            NULL CONSTRAINT FK_Delivery_Customer REFERENCES stm.Customer (CustomerId),
    Method           varchar(10)    NOT NULL,
    Target           nvarchar(1000) NOT NULL,
    DocumentName     nvarchar(260)  NOT NULL,
    ArchivePath      nvarchar(400)  NULL,
    Status           varchar(10)    NOT NULL,
    Attempts         int            NOT NULL CONSTRAINT DF_Delivery_Attempts DEFAULT (0),
    NextAttemptAt    datetime2(0)   NULL,
    LastError        nvarchar(2000) NULL,
    CreatedAt        datetime2(0)   NOT NULL CONSTRAINT DF_Delivery_Created DEFAULT (SYSDATETIME()),
    DeliveredAt      datetime2(0)   NULL,
    CONSTRAINT CK_Delivery_Method CHECK (Method IN ('EMAIL', 'SHARE', 'PRINT'))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Delivery_Retry')
    CREATE INDEX IX_Delivery_Retry ON stm.Delivery (NextAttemptAt) WHERE Status = 'Retry';
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Delivery_Run')
    CREATE INDEX IX_Delivery_Run ON stm.Delivery (RunId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Delivery_Customer')
    CREATE INDEX IX_Delivery_Customer ON stm.Delivery (CustomerId, CreatedAt DESC);
GO

IF OBJECT_ID(N'stm.ApiClient') IS NULL
CREATE TABLE stm.ApiClient
(
    ClientId         int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApiClient PRIMARY KEY,
    Name             nvarchar(60)   NOT NULL,
    KeyPrefix        varchar(20)    NOT NULL CONSTRAINT UQ_ApiClient_Prefix UNIQUE,
    KeyHash          char(64)       NOT NULL,
    AllowedReports   varchar(200)   NOT NULL,
    Enabled          bit            NOT NULL CONSTRAINT DF_ApiClient_Enabled DEFAULT (1),
    CreatedBy        nvarchar(100)  NOT NULL,
    CreatedAt        datetime2(0)   NOT NULL CONSTRAINT DF_ApiClient_Created DEFAULT (SYSDATETIME()),
    LastUsedAt       datetime2(0)   NULL
);
GO

IF OBJECT_ID(N'stm.DocumentRequest') IS NULL
CREATE TABLE stm.DocumentRequest
(
    RequestId        bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_DocumentRequest PRIMARY KEY,
    RequestedAt      datetime2(3)   NOT NULL CONSTRAINT DF_DocumentRequest_At DEFAULT (SYSDATETIME()),
    Channel          varchar(10)    NOT NULL,
    RequestedBy      nvarchar(100)  NOT NULL,
    ReportCode       varchar(20)    NOT NULL,
    Parameters       nvarchar(1000) NULL,
    OutputFormat     varchar(10)    NOT NULL,
    Bytes            int            NULL,
    DurationMs       int            NULL,
    Succeeded        bit            NOT NULL,
    Error            nvarchar(1000) NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DocumentRequest_At')
    CREATE INDEX IX_DocumentRequest_At ON stm.DocumentRequest (RequestedAt DESC);
GO

-- Claims due subscriptions for one distribution host. Expired claims (host stopped during a run) are taken over.
CREATE OR ALTER PROCEDURE stm.ClaimDueSubscriptions
    @Host nvarchar(60),
    @Now datetime2(0),
    @LeaseMinutes int = 60,
    @MaxCount int = 5
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE s
       SET ClaimedBy = @Host,
           ClaimedUntil = DATEADD(MINUTE, @LeaseMinutes, @Now)
    OUTPUT inserted.SubscriptionId, inserted.NextRunAt
      FROM stm.Subscription s
     WHERE s.SubscriptionId IN
           (SELECT TOP (@MaxCount) x.SubscriptionId
              FROM stm.Subscription x WITH (UPDLOCK, READPAST)
             WHERE x.Enabled = 1
               AND x.NextRunAt <= @Now
               AND (x.ClaimedUntil IS NULL OR x.ClaimedUntil < @Now)
             ORDER BY x.NextRunAt);
END
GO

CREATE OR ALTER VIEW stm.CustomerBalance
AS
SELECT c.CustomerId, c.CustomerNo, c.Name, c.CreditController, c.CreditLimit, c.OnHold, c.Delivery,
       ISNULL(SUM(e.OpenAmount), 0) AS OpenBalance,
       ISNULL(SUM(CASE WHEN e.OpenAmount > 0 AND e.DueDate < CAST(SYSDATETIME() AS date) THEN e.OpenAmount END), 0) AS Overdue,
       MAX(CASE WHEN e.EntryType = 'PAY' THEN e.PostingDate END) AS LastPaymentDate
  FROM stm.Customer c
  LEFT JOIN stm.LedgerEntry e ON e.CustomerId = c.CustomerId
 GROUP BY c.CustomerId, c.CustomerNo, c.Name, c.CreditController, c.CreditLimit, c.OnHold, c.Delivery;
GO
