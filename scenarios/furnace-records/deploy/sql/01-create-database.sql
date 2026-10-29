-- Furnace Records database. Run on sql01 as a sysadmin:
--   sqlcmd -S sql01.corp.local -E -i 01-create-database.sql
SET QUOTED_IDENTIFIER ON;
GO
IF DB_ID(N'FurnaceRecords') IS NULL
    CREATE DATABASE [FurnaceRecords];
GO
ALTER DATABASE [FurnaceRecords] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO
USE [FurnaceRecords];
GO
IF OBJECT_ID(N'fr.Loads') IS NOT NULL
BEGIN
    PRINT 'Schema fr already exists.';
    SET NOEXEC ON;
END
GO
IF SCHEMA_ID(N'fr') IS NULL EXEC(N'CREATE SCHEMA [fr];');
GO


CREATE TABLE [fr].[ApiClients] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [KeyHash] nvarchar(64) NOT NULL,
    [Active] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ApiClients] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [fr].[Events] (
    [Id] bigint NOT NULL IDENTITY,
    [LoadId] int NOT NULL,
    [AtUtc] datetime2 NOT NULL,
    [Kind] nvarchar(20) NOT NULL,
    [Detail] nvarchar(200) NULL,
    CONSTRAINT [PK_Events] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [fr].[Furnaces] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(6) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [FurnaceClass] int NOT NULL,
    [Instrumentation] nvarchar(1) NOT NULL,
    [Active] bit NOT NULL,
    [UniformitySurveyDue] date NULL,
    [AccuracyTestDue] date NULL,
    CONSTRAINT [PK_Furnaces] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [fr].[JobRuns] (
    [Job] nvarchar(40) NOT NULL,
    [LastRunUtc] datetime2 NOT NULL,
    [Result] nvarchar(400) NULL,
    CONSTRAINT [PK_JobRuns] PRIMARY KEY ([Job])
);
GO


CREATE TABLE [fr].[Outbox] (
    [Id] bigint NOT NULL IDENTITY,
    [Topic] nvarchar(40) NOT NULL,
    [Key] nvarchar(40) NOT NULL,
    [PayloadJson] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [NextAttemptUtc] datetime2 NOT NULL,
    [Attempts] int NOT NULL,
    [SentAtUtc] datetime2 NULL,
    [LastError] nvarchar(1000) NULL,
    CONSTRAINT [PK_Outbox] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [fr].[Recipes] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(20) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Specification] nvarchar(100) NULL,
    [SetpointCelsius] float NOT NULL,
    [ToleranceMinusCelsius] float NOT NULL,
    [TolerancePlusCelsius] float NOT NULL,
    [SoakMinutes] int NOT NULL,
    [MaxSoakMinutes] int NULL,
    [MaxRampCelsiusPerMinute] float NULL,
    [MaxQuenchDelaySeconds] int NULL,
    [Active] bit NOT NULL,
    CONSTRAINT [PK_Recipes] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [fr].[Samples] (
    [Id] bigint NOT NULL IDENTITY,
    [LoadId] int NOT NULL,
    [AtUtc] datetime2 NOT NULL,
    [Channel] int NOT NULL,
    [Celsius] float NOT NULL,
    CONSTRAINT [PK_Samples] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [fr].[Channels] (
    [Id] int NOT NULL IDENTITY,
    [FurnaceId] int NOT NULL,
    [Channel] int NOT NULL,
    [Name] nvarchar(50) NOT NULL,
    [Role] nvarchar(20) NOT NULL,
    [Type] nvarchar(2) NOT NULL,
    [SensorSerial] nvarchar(40) NULL,
    [CalibratedOn] date NULL,
    [CalibrationDue] date NULL,
    [Enabled] bit NOT NULL,
    CONSTRAINT [PK_Channels] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Channels_Furnaces_FurnaceId] FOREIGN KEY ([FurnaceId]) REFERENCES [fr].[Furnaces] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [fr].[Loads] (
    [Id] int NOT NULL IDENTITY,
    [LoadNumber] nvarchar(20) NOT NULL,
    [Kind] nvarchar(12) NOT NULL,
    [FurnaceId] int NOT NULL,
    [RecipeId] int NOT NULL,
    [Status] nvarchar(12) NOT NULL,
    [PlannedAtUtc] datetime2 NOT NULL,
    [PlannedBy] nvarchar(100) NULL,
    [StartedAtUtc] datetime2 NULL,
    [EndedAtUtc] datetime2 NULL,
    [Passed] bit NULL,
    [SoakStartUtc] datetime2 NULL,
    [SoakEndUtc] datetime2 NULL,
    [SoakMinutes] float NULL,
    [MaxRampCelsiusPerMinute] float NULL,
    [QuenchDelaySeconds] float NULL,
    [FindingsJson] nvarchar(max) NULL,
    [UniformityCelsius] float NULL,
    [SurveyJson] nvarchar(max) NULL,
    [CertificatePath] nvarchar(260) NULL,
    [DecidedBy] nvarchar(100) NULL,
    [DecidedAtUtc] datetime2 NULL,
    [DecisionComment] nvarchar(1000) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Loads] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Loads_Furnaces_FurnaceId] FOREIGN KEY ([FurnaceId]) REFERENCES [fr].[Furnaces] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Loads_Recipes_RecipeId] FOREIGN KEY ([RecipeId]) REFERENCES [fr].[Recipes] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [fr].[Corrections] (
    [Id] int NOT NULL IDENTITY,
    [FurnaceChannelId] int NOT NULL,
    [AtCelsius] float NOT NULL,
    [CorrectionCelsius] float NOT NULL,
    CONSTRAINT [PK_Corrections] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Corrections_Channels_FurnaceChannelId] FOREIGN KEY ([FurnaceChannelId]) REFERENCES [fr].[Channels] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [fr].[LoadParts] (
    [Id] int NOT NULL IDENTITY,
    [LoadId] int NOT NULL,
    [WorkOrder] nvarchar(20) NOT NULL,
    [PartNumber] nvarchar(40) NOT NULL,
    [Lot] nvarchar(40) NULL,
    [Quantity] int NOT NULL,
    CONSTRAINT [PK_LoadParts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LoadParts_Loads_LoadId] FOREIGN KEY ([LoadId]) REFERENCES [fr].[Loads] ([Id]) ON DELETE CASCADE
);
GO


CREATE UNIQUE INDEX [IX_ApiClients_KeyHash] ON [fr].[ApiClients] ([KeyHash]);
GO


CREATE UNIQUE INDEX [IX_Channels_FurnaceId_Channel] ON [fr].[Channels] ([FurnaceId], [Channel]);
GO


CREATE INDEX [IX_Corrections_FurnaceChannelId] ON [fr].[Corrections] ([FurnaceChannelId]);
GO


CREATE INDEX [IX_Events_LoadId_AtUtc] ON [fr].[Events] ([LoadId], [AtUtc]);
GO


CREATE UNIQUE INDEX [IX_Furnaces_Code] ON [fr].[Furnaces] ([Code]);
GO


CREATE INDEX [IX_LoadParts_LoadId] ON [fr].[LoadParts] ([LoadId]);
GO


CREATE INDEX [IX_Loads_FurnaceId_Status] ON [fr].[Loads] ([FurnaceId], [Status]);
GO


CREATE UNIQUE INDEX [IX_Loads_LoadNumber] ON [fr].[Loads] ([LoadNumber]);
GO


CREATE INDEX [IX_Loads_RecipeId] ON [fr].[Loads] ([RecipeId]);
GO


CREATE INDEX [IX_Outbox_SentAtUtc_NextAttemptUtc] ON [fr].[Outbox] ([SentAtUtc], [NextAttemptUtc]);
GO


CREATE UNIQUE INDEX [IX_Recipes_Code] ON [fr].[Recipes] ([Code]);
GO


CREATE INDEX [IX_Samples_LoadId_AtUtc_Channel] ON [fr].[Samples] ([LoadId], [AtUtc], [Channel]);
GO



SET NOEXEC OFF;
GO
