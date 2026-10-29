-- Document Control schema. Safe to run again.
IF SCHEMA_ID(N'dc') IS NULL EXEC (N'CREATE SCHEMA dc AUTHORIZATION dbo');
GO

IF OBJECT_ID(N'dc.Projects') IS NULL
CREATE TABLE dc.Projects (
    Code     varchar(5)     NOT NULL CONSTRAINT PK_Projects PRIMARY KEY,
    Name     nvarchar(200)  NOT NULL,
    Client   nvarchar(200)  NOT NULL,
    Active   bit            NOT NULL CONSTRAINT DF_Projects_Active DEFAULT (1)
);
GO

IF OBJECT_ID(N'dc.Documents') IS NULL
CREATE TABLE dc.Documents (
    Id            int IDENTITY  NOT NULL CONSTRAINT PK_Documents PRIMARY KEY,
    Number        varchar(30)   NOT NULL CONSTRAINT UQ_Documents_Number UNIQUE,
    ProjectCode   varchar(5)    NOT NULL CONSTRAINT FK_Documents_Projects REFERENCES dc.Projects (Code),
    Discipline    char(2)       NOT NULL,
    DocumentType  char(3)       NOT NULL,
    Title         nvarchar(300) NOT NULL,
    CreatedUtc    datetime2(0)  NOT NULL,
    CreatedBy     nvarchar(100) NOT NULL
);
GO

IF OBJECT_ID(N'dc.Revisions') IS NULL
BEGIN
    CREATE TABLE dc.Revisions (
        Id             int IDENTITY  NOT NULL CONSTRAINT PK_Revisions PRIMARY KEY,
        DocumentId     int           NOT NULL CONSTRAINT FK_Revisions_Documents REFERENCES dc.Documents (Id),
        Code           char(3)       NOT NULL,
        Status         tinyint       NOT NULL,   -- 0 submitted, 1 approved, 2 rejected, 3 superseded
        FileName       nvarchar(260) NOT NULL,
        VaultPath      nvarchar(400) NOT NULL,
        Size           bigint        NOT NULL,
        Sha256         char(64)      NOT NULL,
        Source         varchar(10)   NOT NULL,   -- Portal, Drop
        SubmittedBy    nvarchar(100) NOT NULL,
        SubmittedUtc   datetime2(0)  NOT NULL,
        ReviewedBy     nvarchar(100) NULL,
        ReviewedUtc    datetime2(0)  NULL,
        ReviewComment  nvarchar(1000) NULL
    );
    CREATE INDEX IX_Revisions_Document ON dc.Revisions (DocumentId) INCLUDE (Code, Status);
    CREATE INDEX IX_Revisions_Status ON dc.Revisions (Status) WHERE Status = 0;
    -- At most one approved revision per document
    CREATE UNIQUE INDEX UX_Revisions_Approved ON dc.Revisions (DocumentId) WHERE Status = 1;
END
GO

IF OBJECT_ID(N'dc.Transmittals') IS NULL
CREATE TABLE dc.Transmittals (
    Id               int IDENTITY   NOT NULL CONSTRAINT PK_Transmittals PRIMARY KEY,
    Number           varchar(20)    NOT NULL CONSTRAINT UQ_Transmittals_Number UNIQUE,
    ProjectCode      varchar(5)     NOT NULL CONSTRAINT FK_Transmittals_Projects REFERENCES dc.Projects (Code),
    Purpose          tinyint        NOT NULL,  -- 0 IFR, 1 IFA, 2 IFC, 3 IFI
    RecipientCompany nvarchar(200)  NOT NULL,
    RecipientName    nvarchar(200)  NOT NULL,
    RecipientEmail   nvarchar(256)  NOT NULL,
    Notes            nvarchar(1000) NULL,
    Status           tinyint        NOT NULL,  -- 0 queued, 1 building, 2 issued, 3 failed, 4 cancelled
    CreatedBy        nvarchar(100)  NOT NULL,
    CreatedByEmail   nvarchar(256)  NOT NULL,
    CreatedUtc       datetime2(0)   NOT NULL,
    IssuedUtc        datetime2(0)   NULL,
    PackagePath      nvarchar(400)  NULL,
    PackageSize      bigint         NULL,
    Error            nvarchar(2000) NULL,
    Attempts         int            NOT NULL CONSTRAINT DF_Transmittals_Attempts DEFAULT (0),
    ClaimedBy        nvarchar(100)  NULL,
    ClaimedUtc       datetime2(0)   NULL
);
GO

IF OBJECT_ID(N'dc.TransmittalItems') IS NULL
CREATE TABLE dc.TransmittalItems (
    TransmittalId  int NOT NULL CONSTRAINT FK_TransmittalItems_Transmittals REFERENCES dc.Transmittals (Id),
    RevisionId     int NOT NULL CONSTRAINT FK_TransmittalItems_Revisions REFERENCES dc.Revisions (Id),
    CONSTRAINT PK_TransmittalItems PRIMARY KEY (TransmittalId, RevisionId)
);
GO

IF OBJECT_ID(N'dc.DropFiles') IS NULL
BEGIN
    CREATE TABLE dc.DropFiles (
        Id            int IDENTITY   NOT NULL CONSTRAINT PK_DropFiles PRIMARY KEY,
        FileId        varchar(40)    NOT NULL,
        Sha256        char(64)       NOT NULL,
        OriginalPath  nvarchar(400)  NOT NULL,
        Result        varchar(10)    NOT NULL,
        Reason        nvarchar(1000) NULL,
        RevisionId    int            NULL CONSTRAINT FK_DropFiles_Revisions REFERENCES dc.Revisions (Id),
        ProcessedUtc  datetime2(0)   NOT NULL
    );
    CREATE INDEX IX_DropFiles_File ON dc.DropFiles (FileId, Sha256);
END
GO

IF OBJECT_ID(N'dc.Counters') IS NULL
CREATE TABLE dc.Counters (
    Name   varchar(20) NOT NULL CONSTRAINT PK_Counters PRIMARY KEY,
    Value  int         NOT NULL
);
GO
