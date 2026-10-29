-- Customer Care schema. Run in the CustomerCare database.
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF SCHEMA_ID(N'care') IS NULL EXEC(N'CREATE SCHEMA care AUTHORIZATION dbo');
GO

CREATE TABLE care.Queue
(
    Id              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Queue PRIMARY KEY,
    Code            varchar(12)    NOT NULL CONSTRAINT UQ_Queue_Code UNIQUE,
    Name            nvarchar(100)  NOT NULL,
    Email           nvarchar(254)  NULL,
    DefaultPriority int            NOT NULL CONSTRAINT DF_Queue_DefaultPriority DEFAULT (2),
    IsActive        bit            NOT NULL CONSTRAINT DF_Queue_IsActive DEFAULT (1),
    SortOrder       int            NOT NULL CONSTRAINT DF_Queue_SortOrder DEFAULT (100)
);

CREATE TABLE care.Category
(
    Code      varchar(10)   NOT NULL CONSTRAINT PK_Category PRIMARY KEY,
    Name      nvarchar(100) NOT NULL,
    QueueCode varchar(12)   NULL,
    IsActive  bit           NOT NULL CONSTRAINT DF_Category_IsActive DEFAULT (1)
);

CREATE TABLE care.RoutingRule
(
    Id               int IDENTITY(1,1) NOT NULL CONSTRAINT PK_RoutingRule PRIMARY KEY,
    Sequence         int          NOT NULL,
    CategoryCode     varchar(10)  NULL,
    PostalCodePrefix varchar(5)   NULL,
    Segment          varchar(3)   NULL,
    QueueCode        varchar(12)  NOT NULL
);

CREATE TABLE care.SlaTarget
(
    Priority      int   NOT NULL CONSTRAINT PK_SlaTarget PRIMARY KEY,
    ResponseHours float NOT NULL,
    ResolveHours  float NOT NULL
);

CREATE TABLE care.Holiday
(
    [Date] date          NOT NULL CONSTRAINT PK_Holiday PRIMARY KEY,
    Name   nvarchar(100) NOT NULL
);

CREATE TABLE care.Customer
(
    Id                 int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customer PRIMARY KEY,
    AccountNumber      char(10)       NOT NULL CONSTRAINT UQ_Customer_AccountNumber UNIQUE,
    Name               nvarchar(200)  NOT NULL,
    Email              nvarchar(254)  NULL,
    Phone              varchar(30)    NULL,
    Street             nvarchar(200)  NULL,
    PostalCode         char(5)        NULL,
    City               nvarchar(100)  NULL,
    Segment            char(3)        NOT NULL CONSTRAINT CK_Customer_Segment CHECK (Segment IN ('RES', 'SME', 'LCO')),
    IsPriorityCustomer bit            NOT NULL CONSTRAINT DF_Customer_IsPriority DEFAULT (0),
    CreatedUtc         datetime2(0)   NOT NULL CONSTRAINT DF_Customer_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    ModifiedUtc        datetime2(0)   NOT NULL CONSTRAINT DF_Customer_ModifiedUtc DEFAULT (SYSUTCDATETIME())
);
CREATE INDEX IX_Customer_Name ON care.Customer (Name);

CREATE TABLE care.CustomerImportStaging
(
    BatchId            uniqueidentifier NOT NULL,
    AccountNumber      char(10)         NOT NULL,
    Name               nvarchar(200)    NOT NULL,
    Email              nvarchar(254)    NULL,
    Phone              varchar(30)      NULL,
    Street             nvarchar(200)    NULL,
    PostalCode         char(5)          NULL,
    City               nvarchar(100)    NULL,
    Segment            char(3)          NOT NULL,
    IsPriorityCustomer bit              NOT NULL
);
CREATE CLUSTERED INDEX CIX_CustomerImportStaging ON care.CustomerImportStaging (BatchId, AccountNumber);

CREATE TABLE care.CustomerImportLog
(
    Id         int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CustomerImportLog PRIMARY KEY,
    SourceFile nvarchar(260) NOT NULL,
    Inserted   int           NOT NULL,
    Updated    int           NOT NULL,
    Unchanged  int           NOT NULL,
    ImportedUtc datetime2(0) NOT NULL CONSTRAINT DF_CustomerImportLog_ImportedUtc DEFAULT (SYSUTCDATETIME()),
    ImportedBy nvarchar(128) NOT NULL CONSTRAINT DF_CustomerImportLog_ImportedBy DEFAULT (SUSER_SNAME())
);

CREATE TABLE care.CaseNumberSequence
(
    [Year]   int NOT NULL CONSTRAINT PK_CaseNumberSequence PRIMARY KEY,
    LastValue int NOT NULL
);

CREATE TABLE care.[Case]
(
    Id                int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Case PRIMARY KEY,
    CaseNumber        varchar(14)    NOT NULL CONSTRAINT UQ_Case_CaseNumber UNIQUE,
    CustomerId        int            NOT NULL CONSTRAINT FK_Case_Customer REFERENCES care.Customer (Id),
    QueueId           int            NOT NULL CONSTRAINT FK_Case_Queue REFERENCES care.Queue (Id),
    CategoryCode      varchar(10)    NOT NULL CONSTRAINT FK_Case_Category REFERENCES care.Category (Code),
    Subject           nvarchar(200)  NOT NULL,
    Description       nvarchar(max)  NULL,
    Priority          int            NOT NULL,
    Status            int            NOT NULL,
    Channel           int            NOT NULL,
    AssignedTo        nvarchar(100)  NULL,
    ExternalReference varchar(50)    NULL,
    CreatedUtc        datetime2(0)   NOT NULL,
    CreatedBy         nvarchar(100)  NOT NULL,
    ResponseDueUtc    datetime2(0)   NOT NULL,
    ResolveDueUtc     datetime2(0)   NOT NULL,
    FirstResponseUtc  datetime2(0)   NULL,
    ResolvedUtc       datetime2(0)   NULL,
    ClosedUtc         datetime2(0)   NULL,
    EscalationLevel   int            NOT NULL CONSTRAINT DF_Case_EscalationLevel DEFAULT (0),
    ModifiedUtc       datetime2(0)   NOT NULL,
    RowVersion        rowversion     NOT NULL
);
CREATE INDEX IX_Case_Open ON care.[Case] (Status, QueueId) INCLUDE (ResolveDueUtc, AssignedTo, Priority);
CREATE INDEX IX_Case_Customer ON care.[Case] (CustomerId, CreatedUtc DESC);
CREATE INDEX IX_Case_Closed ON care.[Case] (ClosedUtc) WHERE ClosedUtc IS NOT NULL;
CREATE INDEX IX_Case_ExternalReference ON care.[Case] (ExternalReference) WHERE ExternalReference IS NOT NULL;

CREATE TABLE care.CaseNote
(
    Id         int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CaseNote PRIMARY KEY,
    CaseId     int           NOT NULL CONSTRAINT FK_CaseNote_Case REFERENCES care.[Case] (Id),
    Author     nvarchar(100) NOT NULL,
    Body       nvarchar(max) NOT NULL,
    IsInternal bit           NOT NULL,
    CreatedUtc datetime2(0)  NOT NULL
);
CREATE INDEX IX_CaseNote_Case ON care.CaseNote (CaseId);

CREATE TABLE care.CaseAttachment
(
    Id          int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CaseAttachment PRIMARY KEY,
    CaseId      int            NOT NULL CONSTRAINT FK_CaseAttachment_Case REFERENCES care.[Case] (Id),
    FileName    nvarchar(255)  NOT NULL,
    ContentType varchar(100)   NOT NULL,
    Length      int            NOT NULL,
    Content     varbinary(max) NOT NULL,
    UploadedBy  nvarchar(100)  NOT NULL,
    UploadedUtc datetime2(0)   NOT NULL
);
CREATE INDEX IX_CaseAttachment_Case ON care.CaseAttachment (CaseId);

CREATE TABLE care.CaseArchive
(
    CaseNumber  varchar(14)   NOT NULL CONSTRAINT PK_CaseArchive PRIMARY KEY,
    CustomerId  int           NOT NULL,
    ClosedUtc   datetime2(0)  NULL,
    ArchiveFile nvarchar(400) NOT NULL,
    ArchivedUtc datetime2(0)  NOT NULL CONSTRAINT DF_CaseArchive_ArchivedUtc DEFAULT (SYSUTCDATETIME())
);

CREATE TABLE care.KbArticle
(
    Id           int IDENTITY(1,1) NOT NULL CONSTRAINT PK_KbArticle PRIMARY KEY,
    Slug         varchar(80)    NOT NULL CONSTRAINT UQ_KbArticle_Slug UNIQUE,
    Title        nvarchar(200)  NOT NULL,
    Body         nvarchar(max)  NOT NULL,
    Tags         nvarchar(400)  NULL,
    CategoryCode varchar(10)    NULL,
    IsPublished  bit            NOT NULL,
    Views        int            NOT NULL CONSTRAINT DF_KbArticle_Views DEFAULT (0),
    ModifiedBy   nvarchar(100)  NOT NULL,
    ModifiedUtc  datetime2(0)   NOT NULL
);

CREATE TABLE care.DailyQueueStats
(
    [Day]           date         NOT NULL,
    QueueCode       varchar(12)  NOT NULL,
    OpenCases       int          NOT NULL,
    Breached        int          NOT NULL,
    Created         int          NOT NULL,
    Resolved        int          NOT NULL,
    AvgResolveHours float        NULL,
    CONSTRAINT PK_DailyQueueStats PRIMARY KEY ([Day], QueueCode)
);

CREATE TABLE care.EscalationLog
(
    Id         int IDENTITY(1,1) NOT NULL CONSTRAINT PK_EscalationLog PRIMARY KEY,
    CaseId     int          NOT NULL,
    [Level]    int          NOT NULL,
    NotifiedTo nvarchar(400) NULL,
    CreatedUtc datetime2(0) NOT NULL CONSTRAINT DF_EscalationLog_CreatedUtc DEFAULT (SYSUTCDATETIME())
);
GO
