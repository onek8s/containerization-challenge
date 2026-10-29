SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE TABLE dbo.Adjusters
(
    Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Adjusters PRIMARY KEY,
    Login         NVARCHAR(64)  NOT NULL CONSTRAINT UQ_Adjusters_Login UNIQUE,
    DisplayName   NVARCHAR(128) NOT NULL,
    Email         NVARCHAR(256) NULL,
    Company       NVARCHAR(128) NULL,
    PasswordHash  VARCHAR(200)  NOT NULL,
    IsActive      BIT NOT NULL CONSTRAINT DF_Adjusters_IsActive DEFAULT (1),
    IsSupervisor  BIT NOT NULL CONSTRAINT DF_Adjusters_IsSupervisor DEFAULT (0),
    FailedLogins  INT NOT NULL CONSTRAINT DF_Adjusters_FailedLogins DEFAULT (0),
    LastLoginUtc  DATETIME2(0) NULL
);
GO

CREATE TABLE dbo.Partners
(
    Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Partners PRIMARY KEY,
    UserName      NVARCHAR(64)  NOT NULL CONSTRAINT UQ_Partners_UserName UNIQUE,
    Name          NVARCHAR(128) NOT NULL,
    PasswordHash  VARCHAR(200)  NOT NULL,
    IsActive      BIT NOT NULL CONSTRAINT DF_Partners_IsActive DEFAULT (1)
);
GO

CREATE TABLE dbo.Inspections
(
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Inspections PRIMARY KEY,
    ClaimNumber     VARCHAR(20)   NOT NULL,
    PolicyHolder    NVARCHAR(200) NOT NULL,
    PropertyAddress NVARCHAR(400) NOT NULL,
    PerilCode       CHAR(3)       NULL,
    InspectedOn     DATE          NOT NULL,
    AdjusterId      INT           NOT NULL CONSTRAINT FK_Inspections_Adjusters REFERENCES dbo.Adjusters (Id),
    HandlerEmail    NVARCHAR(256) NULL,
    Status          TINYINT       NOT NULL CONSTRAINT DF_Inspections_Status DEFAULT (0),
    EstimatedLoss   MONEY         NULL,
    Notes           NVARCHAR(MAX) NULL,
    CreatedUtc      DATETIME2(0)  NOT NULL,
    ReportSentUtc   DATETIME2(0)  NULL
);
CREATE INDEX IX_Inspections_Claim ON dbo.Inspections (ClaimNumber);
CREATE INDEX IX_Inspections_Adjuster ON dbo.Inspections (AdjusterId, Status) INCLUDE (InspectedOn);
GO

CREATE TABLE dbo.Photos
(
    Id               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Photos PRIMARY KEY NONCLUSTERED,
    InspectionId     INT            NOT NULL CONSTRAINT FK_Photos_Inspections REFERENCES dbo.Inspections (Id),
    Sequence         INT            NOT NULL,
    OriginalFileName NVARCHAR(260)  NOT NULL,
    Caption          NVARCHAR(400)  NULL,
    StoragePath      NVARCHAR(400)  NOT NULL,
    ThumbnailPath    NVARCHAR(400)  NOT NULL,
    Width            INT            NOT NULL,
    Height           INT            NOT NULL,
    Bytes            BIGINT         NOT NULL,
    TakenUtc         DATETIME2(0)   NULL,
    UploadedBy       NVARCHAR(128)  NOT NULL,
    UploadedUtc      DATETIME2(0)   NOT NULL,
    Purged           BIT            NOT NULL CONSTRAINT DF_Photos_Purged DEFAULT (0)
);
CREATE CLUSTERED INDEX CX_Photos_Inspection ON dbo.Photos (InspectionId, Sequence);
GO

CREATE TABLE dbo.ReportQueue
(
    Id            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReportQueue PRIMARY KEY,
    InspectionId  INT            NOT NULL CONSTRAINT FK_ReportQueue_Inspections REFERENCES dbo.Inspections (Id),
    Recipient     NVARCHAR(256)  NOT NULL,
    RequestedBy   NVARCHAR(128)  NOT NULL,
    RequestedUtc  DATETIME2(0)   NOT NULL,
    Status        TINYINT        NOT NULL,
    Attempts      INT            NOT NULL,
    NotBeforeUtc  DATETIME2(0)   NOT NULL,
    LockedBy      NVARCHAR(128)  NULL,
    LockedUtc     DATETIME2(0)   NULL,
    LastError     NVARCHAR(2000) NULL,
    CompletedUtc  DATETIME2(0)   NULL
);
CREATE INDEX IX_ReportQueue_Due ON dbo.ReportQueue (Status, NotBeforeUtc) INCLUDE (LockedUtc);
GO

CREATE TABLE dbo.AuditLog
(
    Id            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLog PRIMARY KEY,
    InspectionId  INT            NOT NULL,
    Action        NVARCHAR(100)  NOT NULL,
    Actor         NVARCHAR(128)  NOT NULL,
    AtUtc         DATETIME2(0)   NOT NULL,
    Detail        NVARCHAR(1000) NULL
);
CREATE INDEX IX_AuditLog_Inspection ON dbo.AuditLog (InspectionId, AtUtc);
GO
