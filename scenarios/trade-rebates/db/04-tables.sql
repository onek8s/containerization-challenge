:on error exit
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
USE [$(DatabaseName)];
GO

IF OBJECT_ID(N'rebate.Setting', N'U') IS NULL
CREATE TABLE rebate.Setting
(
    Name        varchar(60)    NOT NULL CONSTRAINT PK_Setting PRIMARY KEY,
    Value       nvarchar(400)  NOT NULL,
    Description nvarchar(400)  NULL
);
GO

IF OBJECT_ID(N'rebate.Distributor', N'U') IS NULL
CREATE TABLE rebate.Distributor
(
    DistributorId        int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Distributor PRIMARY KEY,
    Code                 nvarchar(20)   NOT NULL CONSTRAINT UQ_Distributor_Code UNIQUE,
    Name                 nvarchar(100)  NOT NULL,
    ErpVendorNo          nvarchar(20)   NOT NULL,
    Currency             char(3)        NOT NULL CONSTRAINT DF_Distributor_Currency DEFAULT ('EUR'),
    StatementEmail       nvarchar(200)  NULL,
    StatementFolder      nvarchar(60)   NOT NULL,
    BankAccountProtected varbinary(1024) NULL,
    BankAccountMasked    nvarchar(42)   NULL,
    IsActive             bit            NOT NULL CONSTRAINT DF_Distributor_IsActive DEFAULT (1),
    ModifiedBy           nvarchar(128)  NOT NULL CONSTRAINT DF_Distributor_ModifiedBy DEFAULT (SUSER_SNAME()),
    ModifiedAt           datetime2(0)   NOT NULL CONSTRAINT DF_Distributor_ModifiedAt DEFAULT (SYSDATETIME())
);
GO

IF OBJECT_ID(N'rebate.ApiClient', N'U') IS NULL
CREATE TABLE rebate.ApiClient
(
    ApiClientId   int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApiClient PRIMARY KEY,
    DistributorId int           NOT NULL CONSTRAINT FK_ApiClient_Distributor REFERENCES rebate.Distributor (DistributorId),
    Name          nvarchar(100) NOT NULL,
    KeyPrefix     char(8)       NOT NULL,
    KeyHash       binary(32)    NOT NULL CONSTRAINT UQ_ApiClient_KeyHash UNIQUE,
    IsActive      bit           NOT NULL CONSTRAINT DF_ApiClient_IsActive DEFAULT (1),
    CreatedAt     datetime2(0)  NOT NULL CONSTRAINT DF_ApiClient_CreatedAt DEFAULT (SYSDATETIME()),
    LastUsedAt    datetime2(0)  NULL
);
GO

IF OBJECT_ID(N'rebate.Product', N'U') IS NULL
CREATE TABLE rebate.Product
(
    Gtin        char(14)      NOT NULL CONSTRAINT PK_Product PRIMARY KEY,
    Sku         nvarchar(20)  NOT NULL,
    Description nvarchar(200) NOT NULL,
    Brand       nvarchar(60)  NOT NULL,
    Category    nvarchar(40)  NOT NULL,
    IsActive    bit           NOT NULL CONSTRAINT DF_Product_IsActive DEFAULT (1),
    CONSTRAINT CK_Product_Gtin CHECK (Gtin NOT LIKE '%[^0-9]%')
);
GO

IF OBJECT_ID(N'rebate.Agreement', N'U') IS NULL
CREATE TABLE rebate.Agreement
(
    AgreementId   int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Agreement PRIMARY KEY,
    AgreementNo   nvarchar(20)  NOT NULL CONSTRAINT UQ_Agreement_No UNIQUE,
    DistributorId int           NOT NULL CONSTRAINT FK_Agreement_Distributor REFERENCES rebate.Distributor (DistributorId),
    Description   nvarchar(200) NOT NULL,
    Basis         char(1)       NOT NULL CONSTRAINT CK_Agreement_Basis CHECK (Basis IN ('Q', 'V')),  -- Q quantity, V net value
    Category      nvarchar(40)  NULL,                                                              -- NULL = all categories
    Tiers         rebate.TierSchedule NOT NULL,
    TiersText     AS Tiers.ToString(),
    ValidFrom     date          NOT NULL,
    ValidTo       date          NOT NULL,
    CapAmount     decimal(19,2) NULL,
    Status        char(1)       NOT NULL CONSTRAINT DF_Agreement_Status DEFAULT ('D')
                                CONSTRAINT CK_Agreement_Status CHECK (Status IN ('D', 'A', 'C')),
    ApprovedBy    nvarchar(128) NULL,
    ApprovedAt    datetime2(0)  NULL,
    ModifiedBy    nvarchar(128) NOT NULL,
    ModifiedAt    datetime2(0)  NOT NULL CONSTRAINT DF_Agreement_ModifiedAt DEFAULT (SYSDATETIME()),
    RowVer        rowversion    NOT NULL,
    CONSTRAINT CK_Agreement_Dates CHECK (ValidTo >= ValidFrom)
);
GO

IF OBJECT_ID(N'rebate.Claim', N'U') IS NULL
CREATE TABLE rebate.Claim
(
    ClaimId       int IDENTITY(1000,1) NOT NULL CONSTRAINT PK_Claim PRIMARY KEY,
    DistributorId int           NOT NULL CONSTRAINT FK_Claim_Distributor REFERENCES rebate.Distributor (DistributorId),
    Period        int           NOT NULL,
    Source        varchar(10)   NOT NULL CONSTRAINT CK_Claim_Source CHECK (Source IN ('API', 'FILE', 'WORKBOOK', 'PORTAL')),
    SourceRef     nvarchar(400) NULL,
    Status        char(1)       NOT NULL CONSTRAINT DF_Claim_Status DEFAULT ('R')
                                CONSTRAINT CK_Claim_Status CHECK (Status IN ('R', 'V', 'X', 'A', 'C')),
    LineCount     int           NOT NULL CONSTRAINT DF_Claim_LineCount DEFAULT (0),
    TotalQuantity decimal(19,4) NOT NULL CONSTRAINT DF_Claim_TotalQuantity DEFAULT (0),
    TotalAmount   decimal(19,2) NOT NULL CONSTRAINT DF_Claim_TotalAmount DEFAULT (0),
    WarningCount  int           NOT NULL CONSTRAINT DF_Claim_WarningCount DEFAULT (0),
    RejectedCount int           NOT NULL CONSTRAINT DF_Claim_RejectedCount DEFAULT (0),
    ReceivedAt    datetime2(0)  NOT NULL CONSTRAINT DF_Claim_ReceivedAt DEFAULT (SYSDATETIME()),
    ReceivedBy    nvarchar(128) NOT NULL,
    ValidatedAt   datetime2(0)  NULL,
    DecidedBy     nvarchar(128) NULL,
    DecidedAt     datetime2(0)  NULL,
    DecisionNote  nvarchar(400) NULL,
    RowVer        rowversion    NOT NULL,
    CONSTRAINT CK_Claim_Period CHECK (Period BETWEEN 200001 AND 209912 AND Period % 100 BETWEEN 1 AND 12)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Claim_Distributor_Period')
    CREATE INDEX IX_Claim_Distributor_Period ON rebate.Claim (DistributorId, Period) INCLUDE (Status, TotalAmount);
GO

IF OBJECT_ID(N'rebate.ClaimLine', N'U') IS NULL
CREATE TABLE rebate.ClaimLine
(
    ClaimId      int           NOT NULL CONSTRAINT FK_ClaimLine_Claim REFERENCES rebate.Claim (ClaimId),
    LineNumber       int           NOT NULL,
    InvoiceNo    nvarchar(20)  NOT NULL,
    InvoiceDate  date          NOT NULL,
    CustomerNo   nvarchar(20)  NOT NULL,
    Gtin         char(14)      NOT NULL,
    Quantity     decimal(19,4) NOT NULL,
    NetAmount    decimal(19,2) NOT NULL,
    ListNetPrice decimal(19,4) NULL,
    LineStatus   char(1)       NOT NULL CONSTRAINT DF_ClaimLine_Status DEFAULT ('P')
                               CONSTRAINT CK_ClaimLine_Status CHECK (LineStatus IN ('P', 'K', 'W', 'X')),  -- pending, ok, warning, rejected
    Issue        nvarchar(200) NULL,
    CONSTRAINT PK_ClaimLine PRIMARY KEY (ClaimId, LineNumber)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ClaimLine_Invoice')
    CREATE INDEX IX_ClaimLine_Invoice ON rebate.ClaimLine (InvoiceNo, Gtin) INCLUDE (LineStatus);
GO

IF OBJECT_ID(N'rebate.ClaimIssue', N'U') IS NULL
CREATE TABLE rebate.ClaimIssue
(
    ClaimIssueId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClaimIssue PRIMARY KEY,
    ClaimId      int           NOT NULL CONSTRAINT FK_ClaimIssue_Claim REFERENCES rebate.Claim (ClaimId),
    LineNumber   int           NOT NULL,
    Message      nvarchar(400) NOT NULL
);
GO

IF OBJECT_ID(N'rebate.Accrual', N'U') IS NULL
CREATE TABLE rebate.Accrual
(
    AgreementId     int           NOT NULL CONSTRAINT FK_Accrual_Agreement REFERENCES rebate.Agreement (AgreementId),
    Period          int           NOT NULL,
    Quantity        decimal(19,4) NOT NULL,
    NetAmount       decimal(19,2) NOT NULL,
    YtdQuantity     decimal(19,4) NOT NULL,
    YtdNetAmount    decimal(19,2) NOT NULL,
    RatePercent     decimal(9,4)  NOT NULL,
    YtdRebate       decimal(19,2) NOT NULL,
    AccruedAmount   decimal(19,2) NOT NULL,
    ToNextTier      decimal(19,4) NULL,
    CalculatedAt    datetime2(0)  NOT NULL CONSTRAINT DF_Accrual_CalculatedAt DEFAULT (SYSDATETIME()),
    StatementId     int           NULL,
    CONSTRAINT PK_Accrual PRIMARY KEY (AgreementId, Period)
);
GO

IF OBJECT_ID(N'rebate.Statement', N'U') IS NULL
CREATE TABLE rebate.Statement
(
    StatementId        int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Statement PRIMARY KEY,
    DistributorId      int              NOT NULL CONSTRAINT FK_Statement_Distributor REFERENCES rebate.Distributor (DistributorId),
    Quarter            char(6)          NOT NULL,   -- 2026Q3
    Amount             decimal(19,2)    NOT NULL,
    Status             char(1)          NOT NULL CONSTRAINT DF_Statement_Status DEFAULT ('Q')
                                        CONSTRAINT CK_Statement_Status CHECK (Status IN ('Q', 'R', 'P', 'F')),
    RequestedBy        nvarchar(128)    NOT NULL,
    RequestedAt        datetime2(0)     NOT NULL CONSTRAINT DF_Statement_RequestedAt DEFAULT (SYSDATETIME()),
    ConversationHandle uniqueidentifier NULL,
    FilePath           nvarchar(400)    NULL,
    ErpDocumentNo      nvarchar(20)     NULL,
    CompletedAt        datetime2(0)     NULL,
    ErrorMessage       nvarchar(2000)   NULL,
    CONSTRAINT CK_Statement_Quarter CHECK (Quarter LIKE '20[0-9][0-9]Q[1-4]')
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Statement_Distributor_Quarter')
    CREATE UNIQUE INDEX UX_Statement_Distributor_Quarter ON rebate.Statement (DistributorId, Quarter) WHERE Status <> 'F';
GO

IF OBJECT_ID(N'rebate.AuditLog', N'U') IS NULL
CREATE TABLE rebate.AuditLog
(
    AuditId  bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLog PRIMARY KEY,
    At       datetime2(3)  NOT NULL CONSTRAINT DF_AuditLog_At DEFAULT (SYSDATETIME()),
    Actor    nvarchar(128) NOT NULL,
    Action   varchar(40)   NOT NULL,
    Entity   varchar(40)   NOT NULL,
    EntityId nvarchar(40)  NULL,
    Detail   nvarchar(2000) NULL
);
GO

-- Loaded by the sell-out workbook import (Excel sheet SellOut).
IF OBJECT_ID(N'stage.SellOutWorkbook', N'U') IS NULL
CREATE TABLE stage.SellOutWorkbook
(
    FileName        nvarchar(260) NOT NULL,
    RowNo           int           NOT NULL,
    DistributorCode nvarchar(255) NULL,
    Period          nvarchar(255) NULL,
    InvoiceNo       nvarchar(255) NULL,
    InvoiceDate     nvarchar(255) NULL,
    CustomerNo      nvarchar(255) NULL,
    Gtin            nvarchar(255) NULL,
    Quantity        nvarchar(255) NULL,
    NetAmount       nvarchar(255) NULL,
    LoadedAt        datetime2(0)  NOT NULL CONSTRAINT DF_SellOutWorkbook_LoadedAt DEFAULT (SYSDATETIME())
);
GO

IF TYPE_ID(N'rebate.ClaimLineTable') IS NULL
CREATE TYPE rebate.ClaimLineTable AS TABLE
(
    LineNumber      int           NOT NULL PRIMARY KEY,
    InvoiceNo   nvarchar(20)  NOT NULL,
    InvoiceDate date          NOT NULL,
    CustomerNo  nvarchar(20)  NOT NULL,
    Gtin        nvarchar(14)  NOT NULL,
    Quantity    decimal(19,4) NOT NULL,
    NetAmount   decimal(19,2) NOT NULL
);
GO
