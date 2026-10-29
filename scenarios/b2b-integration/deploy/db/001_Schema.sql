/*
    VanArsdelIntegration database: partner directory, interchange register, duplicate keys, control numbers,
    SLA definitions and alert state, resubmission log, gateway credentials and tracking portal users.
    Run with sqlcmd (Install-Database.ps1). Idempotent: objects are created when missing.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.TradingPartner', N'U') IS NULL
CREATE TABLE dbo.TradingPartner
(
    PartnerId               nvarchar(20)    NOT NULL CONSTRAINT PK_TradingPartner PRIMARY KEY,
    Name                    nvarchar(100)   NOT NULL,
    RoleId                  tinyint         NOT NULL,   -- 1 customer, 2 supplier, 3 logistics
    StandardId              tinyint         NOT NULL,   -- 1 X12, 2 EDIFACT, 3 XML
    TransportId             tinyint         NOT NULL,   -- 1 AS2, 2 SFTP, 3 FTP, 4 gateway, 5 file
    InterchangeQualifier    nvarchar(4)     NULL,
    InterchangeId           nvarchar(35)    NULL,
    StandardVersion         nvarchar(10)    NULL,
    Gln                     char(13)        NULL,
    ErpNumber               nvarchar(10)    NULL,
    As2Identifier           nvarchar(128)   NULL,
    CurrencyCode            char(3)         NOT NULL CONSTRAINT DF_TradingPartner_Currency DEFAULT ('USD'),
    CountryCode             char(2)         NOT NULL CONSTRAINT DF_TradingPartner_Country DEFAULT ('US'),
    DefaultShipVia          nvarchar(10)    NULL,
    AckRequired             bit             NOT NULL CONSTRAINT DF_TradingPartner_AckRequired DEFAULT (0),
    AckTimeoutMinutes       int             NOT NULL CONSTRAINT DF_TradingPartner_AckTimeout DEFAULT (0),
    CreditCheckThreshold    decimal(18,2)   NOT NULL CONSTRAINT DF_TradingPartner_Credit DEFAULT (0),
    Active                  bit             NOT NULL CONSTRAINT DF_TradingPartner_Active DEFAULT (1),
    TestIndicator           bit             NOT NULL CONSTRAINT DF_TradingPartner_Test DEFAULT (0),
    ExpectsDailyTraffic     bit             NOT NULL CONSTRAINT DF_TradingPartner_Daily DEFAULT (0),
    ChangedBy               nvarchar(100)   NOT NULL CONSTRAINT DF_TradingPartner_ChangedBy DEFAULT (SUSER_SNAME()),
    ChangedUtc              datetime2(3)    NOT NULL CONSTRAINT DF_TradingPartner_ChangedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_TradingPartner_Role CHECK (RoleId BETWEEN 1 AND 3),
    CONSTRAINT CK_TradingPartner_Standard CHECK (StandardId BETWEEN 1 AND 3),
    CONSTRAINT CK_TradingPartner_Transport CHECK (TransportId BETWEEN 1 AND 5)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_TradingPartner_Interchange')
CREATE UNIQUE INDEX UX_TradingPartner_Interchange ON dbo.TradingPartner (InterchangeQualifier, InterchangeId) WHERE InterchangeId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_TradingPartner_As2')
CREATE UNIQUE INDEX UX_TradingPartner_As2 ON dbo.TradingPartner (As2Identifier) WHERE As2Identifier IS NOT NULL;
GO

IF OBJECT_ID(N'dbo.PartnerXref', N'U') IS NULL
CREATE TABLE dbo.PartnerXref
(
    PartnerId       nvarchar(20)    NOT NULL,   -- '*' = all partners (GTIN to material)
    KindId          tinyint         NOT NULL,   -- 1 item, 2 unit, 3 ship-to, 4 warehouse, 5 carrier, 6 payment terms
    PartnerValue    nvarchar(50)    NOT NULL,
    InternalValue   nvarchar(50)    NOT NULL,
    ValidFrom       date            NULL,
    ValidTo         date            NULL,
    ChangedBy       nvarchar(100)   NOT NULL CONSTRAINT DF_PartnerXref_ChangedBy DEFAULT (SUSER_SNAME()),
    ChangedUtc      datetime2(3)    NOT NULL CONSTRAINT DF_PartnerXref_ChangedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_PartnerXref PRIMARY KEY (PartnerId, KindId, PartnerValue),
    CONSTRAINT CK_PartnerXref_Validity CHECK (ValidTo IS NULL OR ValidFrom IS NULL OR ValidTo >= ValidFrom)
);
GO

IF OBJECT_ID(N'dbo.PartnerChangeLog', N'U') IS NULL
CREATE TABLE dbo.PartnerChangeLog
(
    ChangeId        bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_PartnerChangeLog PRIMARY KEY,
    PartnerId       nvarchar(20)    NOT NULL,
    Change          nvarchar(400)   NOT NULL,
    ChangedBy       nvarchar(100)   NOT NULL,
    ChangedUtc      datetime2(3)    NOT NULL CONSTRAINT DF_PartnerChangeLog_ChangedUtc DEFAULT (SYSUTCDATETIME())
);
GO

IF OBJECT_ID(N'dbo.Interchange', N'U') IS NULL
CREATE TABLE dbo.Interchange
(
    InterchangeKey      bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Interchange PRIMARY KEY,
    PartnerId           nvarchar(20)    NOT NULL CONSTRAINT FK_Interchange_Partner REFERENCES dbo.TradingPartner (PartnerId),
    DirectionId         tinyint         NOT NULL,   -- 1 inbound, 2 outbound
    ControlNumber       nvarchar(14)    NOT NULL,
    GroupControlNumber  nvarchar(14)    NULL,
    TransactionType     nvarchar(10)    NOT NULL,
    TransactionCount    int             NOT NULL CONSTRAINT DF_Interchange_Count DEFAULT (1),
    CreatedUtc          datetime2(3)    NOT NULL,
    AckStatusId         tinyint         NOT NULL,   -- 0 not required, 1 pending, 2 accepted, 3 with errors, 4 rejected, 5 overdue
    AckReceivedUtc      datetime2(3)    NULL,
    AckDueUtc           datetime2(3)    NULL,
    FileName            nvarchar(260)   NULL,
    BodyHash            char(64)        NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Interchange_Partner_Control')
CREATE INDEX IX_Interchange_Partner_Control ON dbo.Interchange (PartnerId, ControlNumber) INCLUDE (GroupControlNumber, AckStatusId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Interchange_Pending')
CREATE INDEX IX_Interchange_Pending ON dbo.Interchange (AckDueUtc) INCLUDE (PartnerId, ControlNumber) WHERE AckStatusId = 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Interchange_Created')
CREATE INDEX IX_Interchange_Created ON dbo.Interchange (CreatedUtc) INCLUDE (PartnerId, DirectionId, TransactionType);
GO

IF OBJECT_ID(N'dbo.ReceivedInterchange', N'U') IS NULL
CREATE TABLE dbo.ReceivedInterchange
(
    PartnerId       nvarchar(20)    NOT NULL,
    DuplicateKey    nvarchar(100)   NOT NULL,   -- body hash, ISA13/UNB5 or GW|<batch id>
    ReceivedUtc     datetime2(3)    NOT NULL,
    CONSTRAINT PK_ReceivedInterchange PRIMARY KEY (PartnerId, DuplicateKey)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ReceivedInterchange_Received')
CREATE INDEX IX_ReceivedInterchange_Received ON dbo.ReceivedInterchange (ReceivedUtc);
GO

IF OBJECT_ID(N'dbo.ControlNumber', N'U') IS NULL
CREATE TABLE dbo.ControlNumber
(
    PartnerId       nvarchar(20)    NOT NULL,
    Level           nvarchar(20)    NOT NULL,   -- Interchange, GS, ...
    LastValue       bigint          NOT NULL,
    MaxValue        bigint          NOT NULL CONSTRAINT DF_ControlNumber_Max DEFAULT (999999999),
    CONSTRAINT PK_ControlNumber PRIMARY KEY (PartnerId, Level)
);
GO

IF OBJECT_ID(N'dbo.SlaDefinition', N'U') IS NULL
CREATE TABLE dbo.SlaDefinition
(
    Code                nvarchar(30)    NOT NULL CONSTRAINT PK_SlaDefinition PRIMARY KEY,
    KindId              tinyint         NOT NULL,
    Description         nvarchar(200)   NOT NULL,
    PartnerId           nvarchar(20)    NULL,
    ThresholdMinutes    int             NOT NULL CONSTRAINT DF_SlaDefinition_Minutes DEFAULT (0),
    ThresholdCount      int             NOT NULL CONSTRAINT DF_SlaDefinition_Count DEFAULT (0),
    SeverityId          tinyint         NOT NULL,   -- 1 information, 2 warning, 3 critical
    BusinessHoursOnly   bit             NOT NULL CONSTRAINT DF_SlaDefinition_Business DEFAULT (0),
    NotifyGroup         nvarchar(30)    NOT NULL,
    Enabled             bit             NOT NULL CONSTRAINT DF_SlaDefinition_Enabled DEFAULT (1)
);
GO

IF OBJECT_ID(N'dbo.Holiday', N'U') IS NULL
CREATE TABLE dbo.Holiday
(
    HolidayDate     date            NOT NULL CONSTRAINT PK_Holiday PRIMARY KEY,
    Name            nvarchar(60)    NOT NULL
);
GO

IF OBJECT_ID(N'dbo.NotifyGroup', N'U') IS NULL
CREATE TABLE dbo.NotifyGroup
(
    GroupName       nvarchar(30)    NOT NULL CONSTRAINT PK_NotifyGroup PRIMARY KEY,
    MailAddresses   nvarchar(400)   NOT NULL    -- ';' separated
);
GO

IF OBJECT_ID(N'dbo.AlertState', N'U') IS NULL
CREATE TABLE dbo.AlertState
(
    AlertKey        nvarchar(200)   NOT NULL CONSTRAINT PK_AlertState PRIMARY KEY,
    FirstRaisedUtc  datetime2(3)    NOT NULL,
    LastNotifiedUtc datetime2(3)    NOT NULL,
    NotifyCount     int             NOT NULL,
    SeverityId      tinyint         NOT NULL,
    Subject         nvarchar(300)   NOT NULL,
    NotifyGroup     nvarchar(30)    NOT NULL,
    ClearedUtc      datetime2(3)    NULL
);
GO

IF OBJECT_ID(N'dbo.AlertLog', N'U') IS NULL
CREATE TABLE dbo.AlertLog
(
    AlertLogId      bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AlertLog PRIMARY KEY,
    AlertKey        nvarchar(200)   NOT NULL,
    SlaCode         nvarchar(30)    NOT NULL,
    SeverityId      tinyint         NOT NULL,
    PartnerId       nvarchar(20)    NULL,
    Subject         nvarchar(300)   NOT NULL,
    Detail          nvarchar(max)   NULL,
    RaisedUtc       datetime2(3)    NOT NULL
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AlertLog_Raised')
CREATE INDEX IX_AlertLog_Raised ON dbo.AlertLog (RaisedUtc);
GO

IF OBJECT_ID(N'dbo.ResubmitLog', N'U') IS NULL
CREATE TABLE dbo.ResubmitLog
(
    ResubmitId          bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ResubmitLog PRIMARY KEY,
    OriginalInstanceId  uniqueidentifier NOT NULL,
    OriginalMessageId   uniqueidentifier NOT NULL,
    ServiceName         nvarchar(256)   NULL,
    PartnerId           nvarchar(20)    NULL,
    MessageType         nvarchar(256)   NULL,
    Reason              nvarchar(500)   NOT NULL,
    BodyEdited          bit             NOT NULL,
    ResubmittedBy       nvarchar(100)   NOT NULL,
    ResubmittedUtc      datetime2(3)    NOT NULL CONSTRAINT DF_ResubmitLog_Utc DEFAULT (SYSUTCDATETIME()),
    Target              nvarchar(260)   NOT NULL,
    OriginalTerminated  bit             NOT NULL CONSTRAINT DF_ResubmitLog_Terminated DEFAULT (0)
);
GO

IF OBJECT_ID(N'dbo.ResubmitBody', N'U') IS NULL
CREATE TABLE dbo.ResubmitBody
(
    ResubmitId      bigint          NOT NULL CONSTRAINT PK_ResubmitBody PRIMARY KEY
                                    CONSTRAINT FK_ResubmitBody_Log REFERENCES dbo.ResubmitLog (ResubmitId) ON DELETE CASCADE,
    OriginalBody    varbinary(max)  NULL,
    SubmittedBody   varbinary(max)  NULL
);
GO

IF OBJECT_ID(N'dbo.PartnerCredential', N'U') IS NULL
CREATE TABLE dbo.PartnerCredential
(
    PartnerId       nvarchar(20)    NOT NULL CONSTRAINT PK_PartnerCredential PRIMARY KEY
                                    CONSTRAINT FK_PartnerCredential_Partner REFERENCES dbo.TradingPartner (PartnerId),
    SecretHash      char(64)        NOT NULL,   -- hex SHA-256 of salt + secret
    Salt            nvarchar(64)    NOT NULL,
    Enabled         bit             NOT NULL CONSTRAINT DF_PartnerCredential_Enabled DEFAULT (1),
    ExpiresUtc      datetime2(3)    NULL,
    ChangedUtc      datetime2(3)    NOT NULL CONSTRAINT DF_PartnerCredential_Changed DEFAULT (SYSUTCDATETIME())
);
GO

IF OBJECT_ID(N'dbo.PortalUser', N'U') IS NULL
CREATE TABLE dbo.PortalUser
(
    UserId              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PortalUser PRIMARY KEY,
    UserName            nvarchar(100)   NOT NULL CONSTRAINT UX_PortalUser_UserName UNIQUE,
    DisplayName         nvarchar(100)   NOT NULL,
    Email               nvarchar(200)   NOT NULL,
    PasswordHash        nvarchar(200)   NOT NULL,   -- PBKDF2$iterations$salt$hash
    PartnerId           nvarchar(20)    NULL CONSTRAINT FK_PortalUser_Partner REFERENCES dbo.TradingPartner (PartnerId),
    Roles               nvarchar(200)   NOT NULL,
    Locked              bit             NOT NULL CONSTRAINT DF_PortalUser_Locked DEFAULT (0),
    FailedAttempts      int             NOT NULL CONSTRAINT DF_PortalUser_Failed DEFAULT (0),
    LockedUntilUtc      datetime2(3)    NULL,
    LastLoginUtc        datetime2(3)    NULL,
    PasswordChangedUtc  datetime2(3)    NOT NULL CONSTRAINT DF_PortalUser_PwdChanged DEFAULT (SYSUTCDATETIME()),
    MustChangePassword  bit             NOT NULL CONSTRAINT DF_PortalUser_MustChange DEFAULT (1),
    ChangedBy           nvarchar(100)   NOT NULL,
    ChangedUtc          datetime2(3)    NOT NULL CONSTRAINT DF_PortalUser_Changed DEFAULT (SYSUTCDATETIME())
);
GO

IF OBJECT_ID(N'dbo.PortalLogin', N'U') IS NULL
CREATE TABLE dbo.PortalLogin
(
    LoginId         bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_PortalLogin PRIMARY KEY,
    UserName        nvarchar(100)   NOT NULL,
    Succeeded       bit             NOT NULL,
    AtUtc           datetime2(3)    NOT NULL,
    ClientAddress   nvarchar(64)    NULL
);
GO
