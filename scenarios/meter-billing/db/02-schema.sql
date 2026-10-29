SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE MeterBilling;
GO
IF SCHEMA_ID(N'bill') IS NULL EXEC(N'CREATE SCHEMA bill');
GO

IF OBJECT_ID(N'bill.Tariff') IS NULL
CREATE TABLE bill.Tariff
(
    TariffId             int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Tariff PRIMARY KEY,
    Code                 varchar(10)   NOT NULL,
    Name                 nvarchar(80)  NOT NULL,
    ValidFrom            date          NOT NULL,
    StandingChargePerDay decimal(9,5)  NOT NULL,
    WastewaterPrice      decimal(9,5)  NOT NULL,
    WastewaterShare      decimal(5,3)  NOT NULL CONSTRAINT DF_Tariff_WastewaterShare DEFAULT (1),
    VatRate              decimal(5,2)  NOT NULL,
    CONSTRAINT UQ_Tariff_Code_ValidFrom UNIQUE (Code, ValidFrom)
);
GO

IF OBJECT_ID(N'bill.TariffBlock') IS NULL
CREATE TABLE bill.TariffBlock
(
    TariffId int          NOT NULL CONSTRAINT FK_TariffBlock_Tariff REFERENCES bill.Tariff (TariffId),
    BlockNo  tinyint      NOT NULL,
    UpTo     decimal(9,3) NULL,
    Price    decimal(9,5) NOT NULL,
    CONSTRAINT PK_TariffBlock PRIMARY KEY (TariffId, BlockNo)
);
GO

IF OBJECT_ID(N'bill.BillCycle') IS NULL
CREATE TABLE bill.BillCycle
(
    CycleCode      varchar(10)  NOT NULL CONSTRAINT PK_BillCycle PRIMARY KEY,
    Name           nvarchar(60) NOT NULL,
    IntervalMonths tinyint      NOT NULL,
    NextCutOff     date         NOT NULL,
    -- an actual read is used when it is at most this many days older than the cut-off
    ReadWindowDays smallint     NOT NULL CONSTRAINT DF_BillCycle_ReadWindowDays DEFAULT (21),
    PaymentDays    smallint     NOT NULL CONSTRAINT DF_BillCycle_PaymentDays DEFAULT (14),
    Active         bit          NOT NULL CONSTRAINT DF_BillCycle_Active DEFAULT (1)
);
GO

IF OBJECT_ID(N'bill.Meter') IS NULL
CREATE TABLE bill.Meter
(
    Serial      varchar(12)  NOT NULL CONSTRAINT PK_Meter PRIMARY KEY,
    Dials       tinyint      NOT NULL,
    Multiplier  decimal(7,3) NOT NULL CONSTRAINT DF_Meter_Multiplier DEFAULT (1),
    SizeCode    varchar(6)   NOT NULL,
    InstalledOn date         NOT NULL
);
GO

IF OBJECT_ID(N'bill.MeterRead') IS NULL
CREATE TABLE bill.MeterRead
(
    ReadId       int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MeterRead PRIMARY KEY,
    MeterSerial  varchar(12)   NOT NULL CONSTRAINT FK_MeterRead_Meter REFERENCES bill.Meter (Serial),
    ReadDate     date          NOT NULL,
    Reading      decimal(11,1) NOT NULL,
    ReadType     tinyint       NOT NULL,   -- 0 actual, 1 customer, 2 estimated
    Source       tinyint       NOT NULL,   -- 0 handheld, 1 AMR, 2 customer, 3 engine
    ReaderCode   varchar(4)    NOT NULL,
    Route        varchar(10)   NULL,
    ReaderId     varchar(20)   NULL,
    ImportFileId int           NULL,
    Status       tinyint       NOT NULL CONSTRAINT DF_MeterRead_Status DEFAULT (0),  -- 0 new, 1 billed, 2 discarded, 3 superseded
    ReceivedAt   datetime2(0)  NOT NULL CONSTRAINT DF_MeterRead_ReceivedAt DEFAULT (SYSDATETIME())
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MeterRead_Meter')
    CREATE INDEX IX_MeterRead_Meter ON bill.MeterRead (MeterSerial, ReadDate) INCLUDE (Status, Reading);
GO

IF OBJECT_ID(N'bill.Account') IS NULL
CREATE TABLE bill.Account
(
    AccountNumber    varchar(10)   NOT NULL CONSTRAINT PK_Account PRIMARY KEY,
    CustomerName     nvarchar(80)  NOT NULL,
    AddressLine1     nvarchar(80)  NOT NULL,
    AddressLine2     nvarchar(80)  NULL,
    PostalCode       varchar(10)   NOT NULL,
    City             nvarchar(40)  NOT NULL,
    TariffCode       varchar(10)   NOT NULL,
    CycleCode        varchar(10)   NOT NULL CONSTRAINT FK_Account_BillCycle REFERENCES bill.BillCycle (CycleCode),
    MeterSerial      varchar(12)   NOT NULL CONSTRAINT FK_Account_Meter REFERENCES bill.Meter (Serial),
    PaymentMethod    tinyint       NOT NULL,   -- 0 invoice, 1 direct debit
    Iban             varchar(34)   NULL,
    MandateId        varchar(35)   NULL,
    MandateSignedOn  date          NULL,
    FirstCollection  bit           NOT NULL CONSTRAINT DF_Account_FirstCollection DEFAULT (1),
    AverageDailyUse  decimal(9,4)  NOT NULL,
    LastBilledReadId int           NULL CONSTRAINT FK_Account_LastBilledRead REFERENCES bill.MeterRead (ReadId),
    Active           bit           NOT NULL CONSTRAINT DF_Account_Active DEFAULT (1)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Account_Cycle')
    CREATE INDEX IX_Account_Cycle ON bill.Account (CycleCode) WHERE Active = 1;
GO

IF OBJECT_ID(N'bill.ImportFile') IS NULL
CREATE TABLE bill.ImportFile
(
    ImportFileId  int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ImportFile PRIMARY KEY,
    FileName      nvarchar(260) NOT NULL,
    Source        tinyint       NOT NULL,
    BatchId       varchar(40)   NULL,
    Sha256        char(64)      NOT NULL,
    ReceivedAt    datetime2(0)  NOT NULL CONSTRAINT DF_ImportFile_ReceivedAt DEFAULT (SYSDATETIME()),
    ReadCount     int           NOT NULL,
    RejectedCount int           NOT NULL,
    Status        tinyint       NOT NULL,  -- 0 imported, 1 rejected, 2 duplicate
    Message       nvarchar(max) NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ImportFile_Sha256')
    CREATE INDEX IX_ImportFile_Sha256 ON bill.ImportFile (Sha256);
GO

IF OBJECT_ID(N'bill.BillRun') IS NULL
CREATE TABLE bill.BillRun
(
    RunId       int IDENTITY(1,1) NOT NULL CONSTRAINT PK_BillRun PRIMARY KEY,
    CycleCode   varchar(10)    NOT NULL CONSTRAINT FK_BillRun_BillCycle REFERENCES bill.BillCycle (CycleCode),
    CutOffDate  date           NOT NULL,
    BillDate    date           NOT NULL,
    Stage       tinyint        NOT NULL,
    CreatedAt   datetime2(0)   NOT NULL CONSTRAINT DF_BillRun_CreatedAt DEFAULT (SYSDATETIME()),
    CompletedAt datetime2(0)   NULL,
    ApprovedBy  nvarchar(128)  NULL,
    HeldBy      nvarchar(128)  NULL,
    Message     nvarchar(1000) NULL,
    PrintFile   nvarchar(260)  NULL,
    DebitFile   nvarchar(260)  NULL
);
GO

IF OBJECT_ID(N'bill.Bill') IS NULL
CREATE TABLE bill.Bill
(
    BillId           int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Bill PRIMARY KEY,
    RunId            int           NOT NULL CONSTRAINT FK_Bill_BillRun REFERENCES bill.BillRun (RunId),
    BillNumber       varchar(12)   NOT NULL CONSTRAINT UQ_Bill_BillNumber UNIQUE,
    AccountNumber    varchar(10)   NOT NULL CONSTRAINT FK_Bill_Account REFERENCES bill.Account (AccountNumber),
    BillDate         date          NOT NULL,
    DueDate          date          NOT NULL,
    PeriodFrom       date          NOT NULL,
    PeriodTo         date          NOT NULL,
    OpeningReadId    int           NOT NULL,
    ClosingReadId    int           NOT NULL,
    OpeningReading   decimal(11,1) NOT NULL,
    ClosingReading   decimal(11,1) NOT NULL,
    Consumption      decimal(11,3) NOT NULL,
    Estimated        bit           NOT NULL,
    NetAmount        decimal(11,2) NOT NULL,
    VatAmount        decimal(11,2) NOT NULL,
    TotalAmount      decimal(11,2) NOT NULL,
    PaymentReference varchar(25)   NOT NULL,
    Status           tinyint       NOT NULL,   -- 0 draft, 1 issued, 2 excluded
    IssuedAt         datetime2(0)  NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Bill_Account')
    CREATE INDEX IX_Bill_Account ON bill.Bill (AccountNumber, BillDate DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Bill_Run')
    CREATE INDEX IX_Bill_Run ON bill.Bill (RunId, Status);
GO

IF OBJECT_ID(N'bill.BillLine') IS NULL
CREATE TABLE bill.BillLine
(
    BillId      int           NOT NULL CONSTRAINT FK_BillLine_Bill REFERENCES bill.Bill (BillId) ON DELETE CASCADE,
    LineNumber  smallint      NOT NULL,
    Code        varchar(8)    NOT NULL,
    Description nvarchar(60)  NOT NULL,
    Quantity    decimal(11,3) NOT NULL,
    Unit        varchar(3)    NOT NULL,
    UnitPrice   decimal(9,5)  NOT NULL,
    Amount      decimal(11,2) NOT NULL,
    VatRate     decimal(5,2)  NOT NULL,
    CONSTRAINT PK_BillLine PRIMARY KEY (BillId, LineNumber)
);
GO

IF OBJECT_ID(N'bill.BillException') IS NULL
CREATE TABLE bill.BillException
(
    ExceptionId   int IDENTITY(1,1) NOT NULL CONSTRAINT PK_BillException PRIMARY KEY,
    RunId         int            NOT NULL CONSTRAINT FK_BillException_BillRun REFERENCES bill.BillRun (RunId),
    AccountNumber varchar(10)    NOT NULL,
    ReadId        int            NULL,
    Code          varchar(20)    NOT NULL,
    Consumption   decimal(11,3)  NULL,
    Expected      decimal(11,3)  NULL,
    Resolution    tinyint        NOT NULL CONSTRAINT DF_BillException_Resolution DEFAULT (0),
    ResolvedBy    nvarchar(128)  NULL,
    ResolvedAt    datetime2(0)   NULL,
    Note          nvarchar(500)  NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BillException_Run')
    CREATE INDEX IX_BillException_Run ON bill.BillException (RunId, Resolution);
GO

IF OBJECT_ID(N'bill.RunEvent') IS NULL
CREATE TABLE bill.RunEvent
(
    EventId  bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RunEvent PRIMARY KEY,
    RunId    int            NULL,
    At       datetime2(0)   NOT NULL CONSTRAINT DF_RunEvent_At DEFAULT (SYSDATETIME()),
    Level    varchar(8)     NOT NULL,
    UserName nvarchar(128)  NULL,
    Message  nvarchar(1000) NOT NULL
);
GO

IF OBJECT_ID(N'bill.ReprintRequest') IS NULL
CREATE TABLE bill.ReprintRequest
(
    RequestId   int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReprintRequest PRIMARY KEY,
    BillNumber  varchar(12)   NOT NULL,
    RequestedBy nvarchar(128) NOT NULL,
    RequestedAt datetime2(0)  NOT NULL CONSTRAINT DF_ReprintRequest_RequestedAt DEFAULT (SYSDATETIME()),
    Reason      nvarchar(200) NULL,
    ProcessedAt datetime2(0)  NULL,
    FileName    nvarchar(260) NULL
);
GO

CREATE OR ALTER VIEW bill.RunOverview
AS
SELECT r.RunId, r.CycleCode, r.CutOffDate, r.BillDate, r.Stage, r.CreatedAt, r.CompletedAt, r.HeldBy, r.ApprovedBy, r.Message,
       (SELECT COUNT(*) FROM bill.Account a WHERE a.CycleCode = r.CycleCode AND a.Active = 1) AS Accounts,
       ISNULL(b.Bills, 0) AS Bills, ISNULL(b.EstimatedBills, 0) AS EstimatedBills, ISNULL(b.TotalAmount, 0) AS TotalAmount,
       (SELECT COUNT(*) FROM bill.BillException e WHERE e.RunId = r.RunId AND e.Resolution = 0) AS OpenExceptions
FROM bill.BillRun r
OUTER APPLY (SELECT COUNT(*) AS Bills, SUM(CASE WHEN x.Estimated = 1 THEN 1 ELSE 0 END) AS EstimatedBills, SUM(x.TotalAmount) AS TotalAmount
             FROM bill.Bill x WHERE x.RunId = r.RunId AND x.Status <> 2) b;
GO
