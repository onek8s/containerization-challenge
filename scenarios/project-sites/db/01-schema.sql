-- ProjectSitesHub: KPI history and subscriptions for the dashboards add-in (schema dash),
-- watermarks and run history for the sync service (schema sync).
-- SQL Server 2014 or later. Run with sqlcmd -S sql01.corp.local -i 01-schema.sql
-- The SharePoint content and configuration databases are on the farm's own SQL alias and are not touched here.

IF DB_ID(N'ProjectSitesHub') IS NULL
BEGIN
    CREATE DATABASE ProjectSitesHub;
    ALTER DATABASE ProjectSitesHub SET RECOVERY SIMPLE;
END
GO

USE ProjectSitesHub;
GO

IF SCHEMA_ID(N'dash') IS NULL EXEC (N'CREATE SCHEMA dash AUTHORIZATION dbo');
IF SCHEMA_ID(N'sync') IS NULL EXEC (N'CREATE SCHEMA sync AUTHORIZATION dbo');
GO

IF OBJECT_ID(N'dash.KpiSnapshot') IS NULL
CREATE TABLE dash.KpiSnapshot
(
    Id                  bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_KpiSnapshot PRIMARY KEY,
    ProjectNumber       nvarchar(10)  NOT NULL,
    SnapshotDate        date          NOT NULL,
    OfficeCode          nvarchar(10)  NULL,
    Overall             nvarchar(12)  NULL,
    Schedule            nvarchar(12)  NULL,
    Budget              nvarchar(12)  NULL,
    Risk                nvarchar(12)  NULL,
    HoursToDate         decimal(12,2) NOT NULL CONSTRAINT DF_KpiSnapshot_Hours DEFAULT (0),
    BudgetHours         decimal(12,2) NOT NULL CONSTRAINT DF_KpiSnapshot_Budget DEFAULT (0),
    BurnRatio           decimal(6,2)  NOT NULL CONSTRAINT DF_KpiSnapshot_Burn DEFAULT (0),
    PercentComplete     decimal(5,2)  NOT NULL CONSTRAINT DF_KpiSnapshot_Pct DEFAULT (0),
    DeliverablesTotal   int NOT NULL CONSTRAINT DF_KpiSnapshot_DelTotal DEFAULT (0),
    DeliverablesIssued  int NOT NULL CONSTRAINT DF_KpiSnapshot_DelIssued DEFAULT (0),
    DeliverablesOverdue int NOT NULL CONSTRAINT DF_KpiSnapshot_DelOverdue DEFAULT (0),
    OpenRfis            int NOT NULL CONSTRAINT DF_KpiSnapshot_Rfis DEFAULT (0),
    OpenHighRisks       int NOT NULL CONSTRAINT DF_KpiSnapshot_Risks DEFAULT (0)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_KpiSnapshot_Project_Date')
    CREATE UNIQUE INDEX UX_KpiSnapshot_Project_Date ON dash.KpiSnapshot (ProjectNumber, SnapshotDate);
GO

IF OBJECT_ID(N'dash.Subscription') IS NULL
CREATE TABLE dash.Subscription
(
    Id            int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Subscription PRIMARY KEY,
    UserLogin     nvarchar(256) NOT NULL,
    ProjectNumber nvarchar(10)  NOT NULL,
    Email         nvarchar(256) NULL,
    Frequency     nvarchar(10)  NULL,
    CreatedUtc    datetime      NOT NULL CONSTRAINT DF_Subscription_Created DEFAULT (GETUTCDATE()),
    LastSentUtc   datetime      NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Subscription_User_Project')
    CREATE UNIQUE INDEX UX_Subscription_User_Project ON dash.Subscription (UserLogin, ProjectNumber);
GO

IF OBJECT_ID(N'dash.UserPreference') IS NULL
CREATE TABLE dash.UserPreference
(
    UserLogin     nvarchar(256) NOT NULL CONSTRAINT PK_UserPreference PRIMARY KEY,
    DefaultOffice nvarchar(10)  NULL,
    DefaultView   nvarchar(10)  NULL,
    ShowClosed    bit NOT NULL CONSTRAINT DF_UserPreference_ShowClosed DEFAULT (0)
);
GO

IF OBJECT_ID(N'sync.Watermark') IS NULL
CREATE TABLE sync.Watermark
(
    Name       nvarchar(64) NOT NULL CONSTRAINT PK_Watermark PRIMARY KEY,
    ValueUtc   datetime     NOT NULL,
    UpdatedUtc datetime     NOT NULL
);
GO

IF OBJECT_ID(N'sync.Run') IS NULL
CREATE TABLE sync.Run
(
    Id          bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Run PRIMARY KEY,
    Job         nvarchar(64)   NOT NULL,
    StartedUtc  datetime       NOT NULL,
    FinishedUtc datetime       NOT NULL,
    Succeeded   bit            NOT NULL,
    Created     int            NOT NULL,
    Updated     int            NOT NULL,
    Failed      int            NOT NULL,
    Message     nvarchar(2000) NULL,
    MachineName nvarchar(64)   NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Run_Job_Started')
    CREATE INDEX IX_Run_Job_Started ON sync.Run (Job, StartedUtc DESC);
GO

-- Run history older than 180 days is removed by the SQL Agent job created below.
IF OBJECT_ID(N'sync.PurgeRuns') IS NULL
    EXEC (N'CREATE PROCEDURE sync.PurgeRuns @Days int = 180 AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM sync.Run WHERE StartedUtc < DATEADD(DAY, -@Days, GETUTCDATE());
END');
GO
