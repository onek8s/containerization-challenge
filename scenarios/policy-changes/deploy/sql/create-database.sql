-- Policy Changes database. Run on sql01 as a sysadmin:
--   sqlcmd -S sql01.corp.local -E -i create-database.sql
IF DB_ID(N'PolicyChanges') IS NULL
    CREATE DATABASE [PolicyChanges];
GO

USE [PolicyChanges];
GO

-- Read committed snapshot keeps portal reads from blocking the desk station queue updates.
ALTER DATABASE [PolicyChanges] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO

CREATE TABLE [dbo].[DeskStations] (
    [Name] nvarchar(50) NOT NULL,
    [UserName] nvarchar(100) NOT NULL,
    [ClientVersion] nvarchar(30) NULL,
    [State] nvarchar(30) NOT NULL,
    [Detail] nvarchar(300) NULL,
    [CurrentReference] nvarchar(20) NULL,
    [LastSeenUtc] datetime2 NOT NULL,
    [AppliedCount] int NOT NULL,
    [FailedCount] int NOT NULL,
    CONSTRAINT [PK_DeskStations] PRIMARY KEY ([Name])
);
GO


CREATE TABLE [dbo].[Partners] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(20) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [ApiKeyHash] nvarchar(64) NOT NULL,
    [Active] bit NOT NULL,
    [CreatedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Partners] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [dbo].[ChangeRequests] (
    [Id] bigint NOT NULL IDENTITY,
    [Reference] nvarchar(20) NOT NULL,
    [PolicyNumber] nvarchar(12) NOT NULL,
    [ChangeType] nvarchar(30) NOT NULL,
    [EffectiveDate] date NOT NULL,
    [FieldsJson] nvarchar(max) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [Channel] nvarchar(10) NOT NULL,
    [SubmittedBy] nvarchar(100) NOT NULL,
    [PartnerId] int NULL,
    [PartnerReference] nvarchar(50) NULL,
    [CreatedUtc] datetime2 NOT NULL,
    [UpdatedUtc] datetime2 NOT NULL,
    [Attempts] int NOT NULL,
    [NextAttemptUtc] datetime2 NOT NULL,
    [LockedBy] nvarchar(50) NULL,
    [LockedUntilUtc] datetime2 NULL,
    [Endorsement] nvarchar(20) NULL,
    [ResultMessage] nvarchar(500) NULL,
    [CompletedUtc] datetime2 NULL,
    CONSTRAINT [PK_ChangeRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ChangeRequests_Partners_PartnerId] FOREIGN KEY ([PartnerId]) REFERENCES [dbo].[Partners] ([Id])
);
GO


CREATE TABLE [dbo].[ChangeEvents] (
    [Id] bigint NOT NULL IDENTITY,
    [ChangeRequestId] bigint NOT NULL,
    [AtUtc] datetime2 NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [Message] nvarchar(500) NOT NULL,
    [By] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_ChangeEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ChangeEvents_ChangeRequests_ChangeRequestId] FOREIGN KEY ([ChangeRequestId]) REFERENCES [dbo].[ChangeRequests] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [dbo].[ChangeEvidence] (
    [Id] bigint NOT NULL IDENTITY,
    [ChangeRequestId] bigint NOT NULL,
    [Attempt] int NOT NULL,
    [Name] nvarchar(50) NOT NULL,
    [RelativePath] nvarchar(260) NOT NULL,
    [CapturedUtc] datetime2 NOT NULL,
    [Station] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_ChangeEvidence] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ChangeEvidence_ChangeRequests_ChangeRequestId] FOREIGN KEY ([ChangeRequestId]) REFERENCES [dbo].[ChangeRequests] ([Id]) ON DELETE CASCADE
);
GO


CREATE INDEX [IX_ChangeEvents_ChangeRequestId] ON [dbo].[ChangeEvents] ([ChangeRequestId]);
GO


CREATE INDEX [IX_ChangeEvidence_ChangeRequestId] ON [dbo].[ChangeEvidence] ([ChangeRequestId]);
GO


CREATE INDEX [IX_ChangeRequests_PartnerId_PartnerReference] ON [dbo].[ChangeRequests] ([PartnerId], [PartnerReference]);
GO


CREATE INDEX [IX_ChangeRequests_PolicyNumber] ON [dbo].[ChangeRequests] ([PolicyNumber]);
GO


CREATE UNIQUE INDEX [IX_ChangeRequests_Reference] ON [dbo].[ChangeRequests] ([Reference]);
GO


CREATE INDEX [IX_ChangeRequests_Status_NextAttemptUtc] ON [dbo].[ChangeRequests] ([Status], [NextAttemptUtc]);
GO


CREATE UNIQUE INDEX [IX_Partners_ApiKeyHash] ON [dbo].[Partners] ([ApiKeyHash]);
GO


CREATE UNIQUE INDEX [IX_Partners_Code] ON [dbo].[Partners] ([Code]);
GO


