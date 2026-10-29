-- Filtered indexes and the procedures that write to them need QUOTED_IDENTIFIER ON (sqlcmd defaults to OFF).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE StaffOnboarding;
GO
IF SCHEMA_ID(N'onb') IS NULL EXEC (N'CREATE SCHEMA onb');
GO
IF SCHEMA_ID(N'idm') IS NULL EXEC (N'CREATE SCHEMA idm');
GO

IF OBJECT_ID(N'onb.RequestCounter') IS NULL
CREATE TABLE onb.RequestCounter
(
    Kind      tinyint NOT NULL,
    [Year]    smallint NOT NULL,
    LastValue int NOT NULL,
    CONSTRAINT PK_RequestCounter PRIMARY KEY (Kind, [Year])
);
GO

-- Kind: 1 Joiner, 2 Mover, 3 Leaver. Status values match StaffOnboarding.Core.Requests.RequestStatus.
IF OBJECT_ID(N'onb.Request') IS NULL
CREATE TABLE onb.Request
(
    RequestNumber      varchar(20)      NOT NULL CONSTRAINT PK_Request PRIMARY KEY,
    Kind               tinyint          NOT NULL,
    Status             tinyint          NOT NULL CONSTRAINT DF_Request_Status DEFAULT (0),
    Subject            nvarchar(200)    NOT NULL,
    SamAccountName     nvarchar(20)     NULL,
    Department         varchar(10)      NULL,
    ManagerAccount     nvarchar(20)     NULL,
    EffectiveDate      date             NOT NULL,
    RequestedBy        nvarchar(100)    NOT NULL,
    SubmittedAtUtc     datetime2(0)     NOT NULL CONSTRAINT DF_Request_Submitted DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc       datetime2(0)     NOT NULL CONSTRAINT DF_Request_Updated DEFAULT (SYSUTCDATETIME()),
    NextStepAtUtc      datetime2(0)     NULL,
    NextStep           nvarchar(100)    NULL,
    WorkflowInstanceId uniqueidentifier NULL,
    Definition         varchar(64)      NULL,
    Payload            nvarchar(max)    NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Request_Status')
    CREATE INDEX IX_Request_Status ON onb.Request (Status, SubmittedAtUtc DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Request_Instance')
    CREATE INDEX IX_Request_Instance ON onb.Request (WorkflowInstanceId) WHERE WorkflowInstanceId IS NOT NULL;
GO

-- Kind: 1 ManagerApproval, 2 ITApproval, 3 HomeFolderHandover. Status: Open, Done, Escalated, Expired, Cancelled.
IF OBJECT_ID(N'onb.ApprovalTask') IS NULL
CREATE TABLE onb.ApprovalTask
(
    TaskId        uniqueidentifier NOT NULL CONSTRAINT PK_ApprovalTask PRIMARY KEY,
    RequestNumber varchar(20)      NOT NULL CONSTRAINT FK_ApprovalTask_Request REFERENCES onb.Request (RequestNumber),
    Kind          tinyint          NOT NULL,
    AssignedTo    nvarchar(100)    NOT NULL,
    CreatedAtUtc  datetime2(0)     NOT NULL CONSTRAINT DF_ApprovalTask_Created DEFAULT (SYSUTCDATETIME()),
    DueAtUtc      datetime2(0)     NOT NULL,
    Status        varchar(20)      NOT NULL CONSTRAINT DF_ApprovalTask_Status DEFAULT ('Open'),
    Outcome       tinyint          NULL,
    DecidedBy     nvarchar(100)    NULL,
    DecidedAtUtc  datetime2(0)     NULL,
    Comment       nvarchar(2000)   NULL,
    Reminders     int              NOT NULL CONSTRAINT DF_ApprovalTask_Reminders DEFAULT (0)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ApprovalTask_Open')
    CREATE INDEX IX_ApprovalTask_Open ON onb.ApprovalTask (AssignedTo, DueAtUtc) WHERE Status = 'Open';
GO

IF OBJECT_ID(N'onb.Timeline') IS NULL
CREATE TABLE onb.Timeline
(
    Id            bigint IDENTITY  NOT NULL CONSTRAINT PK_Timeline PRIMARY KEY,
    RequestNumber varchar(20)      NOT NULL,
    AtUtc         datetime2(3)     NOT NULL CONSTRAINT DF_Timeline_At DEFAULT (SYSUTCDATETIME()),
    Actor         nvarchar(100)    NOT NULL,
    Event         nvarchar(100)    NOT NULL,
    Detail        nvarchar(max)    NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Timeline_Request')
    CREATE INDEX IX_Timeline_Request ON onb.Timeline (RequestNumber, Id);
GO

IF OBJECT_ID(N'onb.Account') IS NULL
CREATE TABLE onb.Account
(
    RequestNumber     varchar(20)   NOT NULL CONSTRAINT PK_Account PRIMARY KEY,
    SamAccountName    nvarchar(20)  NOT NULL,
    UserPrincipalName nvarchar(256) NULL,
    Mail              nvarchar(256) NULL,
    DistinguishedName nvarchar(400) NULL,
    HomeDirectory     nvarchar(260) NULL,
    CreatedAtUtc      datetime2(0)  NOT NULL CONSTRAINT DF_Account_Created DEFAULT (SYSUTCDATETIME())
);
GO

-- Provisioning journal written by the provisioning service on idm01.
IF OBJECT_ID(N'idm.Operation') IS NULL
CREATE TABLE idm.Operation
(
    Id               bigint IDENTITY NOT NULL CONSTRAINT PK_Operation PRIMARY KEY,
    RequestNumber    varchar(20)     NOT NULL,
    Step             varchar(40)     NOT NULL,
    SamAccountName   nvarchar(20)    NULL,
    Caller           nvarchar(100)   NOT NULL,
    Succeeded        bit             NOT NULL,
    DomainController nvarchar(100)   NULL,
    AtUtc            datetime2(3)    NOT NULL CONSTRAINT DF_Operation_At DEFAULT (SYSUTCDATETIME()),
    ResultJson       nvarchar(max)   NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Operation_Done')
    CREATE UNIQUE INDEX UX_Operation_Done ON idm.Operation (RequestNumber, Step) WHERE Succeeded = 1;
GO

CREATE OR ALTER PROCEDURE onb.CreateRequest
    @Kind tinyint, @Subject nvarchar(200), @SamAccountName nvarchar(20), @Department varchar(10), @ManagerAccount nvarchar(20),
    @EffectiveDate date, @RequestedBy nvarchar(100), @Payload nvarchar(max), @RequestNumber varchar(20) OUTPUT
AS
BEGIN
    SET NOCOUNT, XACT_ABORT ON;
    DECLARE @Year smallint = YEAR(SYSUTCDATETIME()), @Value int;
    DECLARE @Prefix char(3) = CASE @Kind WHEN 1 THEN 'ONB' WHEN 2 THEN 'MOV' WHEN 3 THEN 'LVR' END;
    IF @Prefix IS NULL THROW 50001, 'Unknown request kind.', 1;

    BEGIN TRAN;
    UPDATE onb.RequestCounter WITH (UPDLOCK, HOLDLOCK) SET @Value = LastValue = LastValue + 1 WHERE Kind = @Kind AND [Year] = @Year;
    IF @@ROWCOUNT = 0
    BEGIN
        SET @Value = 1;
        INSERT onb.RequestCounter (Kind, [Year], LastValue) VALUES (@Kind, @Year, 1);
    END;

    SET @RequestNumber = @Prefix + '-' + CAST(@Year AS char(4)) + '-' + RIGHT('00000' + CAST(@Value AS varchar(5)), 5);
    INSERT onb.Request (RequestNumber, Kind, Subject, SamAccountName, Department, ManagerAccount, EffectiveDate, RequestedBy, Payload)
    VALUES (@RequestNumber, @Kind, @Subject, @SamAccountName, @Department, @ManagerAccount, @EffectiveDate, @RequestedBy, @Payload);
    INSERT onb.Timeline (RequestNumber, Actor, Event) VALUES (@RequestNumber, @RequestedBy, N'Request created');
    COMMIT;
END;
GO

CREATE OR ALTER PROCEDURE onb.MarkStartFailed @RequestNumber varchar(20), @Error nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE onb.Request SET Status = 9, UpdatedAtUtc = SYSUTCDATETIME() WHERE RequestNumber = @RequestNumber AND WorkflowInstanceId IS NULL;
    INSERT onb.Timeline (RequestNumber, Actor, Event, Detail) VALUES (@RequestNumber, N'portal', N'Workflow start failed', @Error);
END;
GO

CREATE OR ALTER PROCEDURE onb.AttachInstance @RequestNumber varchar(20), @InstanceId uniqueidentifier, @Definition varchar(64)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE onb.Request SET WorkflowInstanceId = @InstanceId, Definition = @Definition, UpdatedAtUtc = SYSUTCDATETIME()
    WHERE RequestNumber = @RequestNumber;
    IF @@ROWCOUNT = 0 THROW 50002, 'Request not found.', 1;
END;
GO

CREATE OR ALTER PROCEDURE onb.SetStatus @RequestNumber varchar(20), @Status tinyint
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE onb.Request SET Status = @Status, UpdatedAtUtc = SYSUTCDATETIME() WHERE RequestNumber = @RequestNumber;
END;
GO

CREATE OR ALTER PROCEDURE onb.SetSchedule @RequestNumber varchar(20), @NextStepAtUtc datetime2(0), @NextStep nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE onb.Request SET NextStepAtUtc = @NextStepAtUtc, NextStep = @NextStep, UpdatedAtUtc = SYSUTCDATETIME()
    WHERE RequestNumber = @RequestNumber;
END;
GO

CREATE OR ALTER PROCEDURE onb.AddTimeline @RequestNumber varchar(20), @Actor nvarchar(100), @Event nvarchar(100), @Detail nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT onb.Timeline (RequestNumber, Actor, Event, Detail) VALUES (@RequestNumber, @Actor, @Event, @Detail);
END;
GO

CREATE OR ALTER PROCEDURE onb.CreateTask @TaskId uniqueidentifier, @RequestNumber varchar(20), @Kind tinyint, @AssignedTo nvarchar(100), @DueAtUtc datetime2(0)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM onb.ApprovalTask WHERE TaskId = @TaskId)
        INSERT onb.ApprovalTask (TaskId, RequestNumber, Kind, AssignedTo, DueAtUtc) VALUES (@TaskId, @RequestNumber, @Kind, @AssignedTo, @DueAtUtc);
END;
GO

CREATE OR ALTER PROCEDURE onb.CloseTask @TaskId uniqueidentifier, @Status varchar(20), @Outcome tinyint, @DecidedBy nvarchar(100), @Comment nvarchar(2000)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE onb.ApprovalTask
    SET Status = @Status, Outcome = @Outcome, DecidedBy = @DecidedBy, Comment = @Comment, DecidedAtUtc = SYSUTCDATETIME()
    WHERE TaskId = @TaskId AND Status = 'Open';
END;
GO

CREATE OR ALTER PROCEDURE onb.CountReminder @TaskId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE onb.ApprovalTask SET Reminders = Reminders + 1 WHERE TaskId = @TaskId;
END;
GO

CREATE OR ALTER PROCEDURE onb.CloseOpenTasks @RequestNumber varchar(20), @Status varchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE onb.ApprovalTask SET Status = @Status, DecidedBy = N'workflow', DecidedAtUtc = SYSUTCDATETIME()
    WHERE RequestNumber = @RequestNumber AND Status = 'Open';
END;
GO

CREATE OR ALTER PROCEDURE onb.SaveAccount @RequestNumber varchar(20), @SamAccountName nvarchar(20), @UserPrincipalName nvarchar(256),
    @Mail nvarchar(256), @DistinguishedName nvarchar(400), @HomeDirectory nvarchar(260)
AS
BEGIN
    SET NOCOUNT ON;
    MERGE onb.Account AS t
    USING (SELECT @RequestNumber AS RequestNumber) AS s ON t.RequestNumber = s.RequestNumber
    WHEN MATCHED THEN UPDATE SET SamAccountName = @SamAccountName, UserPrincipalName = @UserPrincipalName, Mail = @Mail,
        DistinguishedName = @DistinguishedName, HomeDirectory = @HomeDirectory
    WHEN NOT MATCHED THEN INSERT (RequestNumber, SamAccountName, UserPrincipalName, Mail, DistinguishedName, HomeDirectory)
        VALUES (@RequestNumber, @SamAccountName, @UserPrincipalName, @Mail, @DistinguishedName, @HomeDirectory);
END;
GO

CREATE OR ALTER PROCEDURE onb.SetSubjectAccount @RequestNumber varchar(20), @SamAccountName nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE onb.Request SET SamAccountName = @SamAccountName, UpdatedAtUtc = SYSUTCDATETIME() WHERE RequestNumber = @RequestNumber;
END;
GO

CREATE OR ALTER PROCEDURE idm.RecordOperation @RequestNumber varchar(20), @Step varchar(40), @SamAccountName nvarchar(20),
    @Caller nvarchar(100), @Succeeded bit, @DomainController nvarchar(100), @ResultJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    IF @Succeeded = 1 AND EXISTS (SELECT 1 FROM idm.Operation WHERE RequestNumber = @RequestNumber AND Step = @Step AND Succeeded = 1)
        RETURN;
    INSERT idm.Operation (RequestNumber, Step, SamAccountName, Caller, Succeeded, DomainController, ResultJson)
    VALUES (@RequestNumber, @Step, @SamAccountName, @Caller, @Succeeded, @DomainController, @ResultJson);
END;
GO
