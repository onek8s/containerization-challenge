IF SCHEMA_ID(N'permits') IS NULL EXEC(N'CREATE SCHEMA [permits];');
GO


CREATE TABLE [permits].[Applicants] (
    [Id] int NOT NULL IDENTITY,
    [Type] int NOT NULL,
    [EidSubject] nvarchar(100) NULL,
    [PersonalIdHash] nvarchar(64) NULL,
    [CompanyNumber] nvarchar(max) NULL,
    [DisplayName] nvarchar(150) NOT NULL,
    [Email] nvarchar(200) NULL,
    [Phone] nvarchar(max) NULL,
    [PostalAddress] nvarchar(max) NULL,
    [Postcode] nvarchar(10) NULL,
    [PrefersPaper] bit NOT NULL,
    [CreatedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Applicants] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [permits].[Holidays] (
    [Date] date NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Holidays] PRIMARY KEY ([Date])
);
GO


CREATE TABLE [permits].[IntakeItems] (
    [Id] int NOT NULL IDENTITY,
    [SourceFile] nvarchar(450) NOT NULL,
    [Pages] int NOT NULL,
    [FormCode] nvarchar(max) NULL,
    [Confidence] float NOT NULL,
    [ExtractedJson] nvarchar(max) NULL,
    [OcrText] nvarchar(max) NULL,
    [CaseId] int NULL,
    [Status] nvarchar(max) NOT NULL,
    [ReceivedUtc] datetime2 NOT NULL,
    [ProcessedUtc] datetime2 NULL,
    [Error] nvarchar(max) NULL,
    CONSTRAINT [PK_IntakeItems] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [permits].[JobRuns] (
    [Id] bigint NOT NULL IDENTITY,
    [Job] nvarchar(max) NOT NULL,
    [StartedUtc] datetime2 NOT NULL,
    [FinishedUtc] datetime2 NULL,
    [Host] nvarchar(max) NOT NULL,
    [Success] bit NOT NULL,
    [Items] int NOT NULL,
    [Message] nvarchar(max) NULL,
    CONSTRAINT [PK_JobRuns] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [permits].[Outbox] (
    [Id] bigint NOT NULL IDENTITY,
    [Type] nvarchar(max) NOT NULL,
    [Payload] nvarchar(max) NOT NULL,
    [CreatedUtc] datetime2 NOT NULL,
    [ProcessedUtc] datetime2 NULL,
    [Attempts] int NOT NULL,
    [LastError] nvarchar(max) NULL,
    CONSTRAINT [PK_Outbox] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [permits].[PaymentNotifications] (
    [Id] bigint NOT NULL IDENTITY,
    [EventId] nvarchar(450) NOT NULL,
    [Body] nvarchar(max) NOT NULL,
    [ReceivedUtc] datetime2 NOT NULL,
    [SignatureValid] bit NOT NULL,
    [Outcome] nvarchar(max) NULL,
    CONSTRAINT [PK_PaymentNotifications] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [permits].[Sequences] (
    [Name] nvarchar(450) NOT NULL,
    [Year] int NOT NULL,
    [Value] int NOT NULL,
    CONSTRAINT [PK_Sequences] PRIMARY KEY ([Name], [Year])
);
GO


CREATE TABLE [permits].[StandardConditions] (
    [Code] nvarchar(450) NOT NULL,
    [Text] nvarchar(max) NOT NULL,
    [AppliesTo] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_StandardConditions] PRIMARY KEY ([Code])
);
GO


CREATE TABLE [permits].[ZoneTariffs] (
    [ZoneCode] nvarchar(450) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [ResidentAnnual] decimal(10,2) NOT NULL,
    [BusinessAnnual] decimal(10,2) NOT NULL,
    [VisitorBookOf20] decimal(10,2) NOT NULL,
    [MaxPermitsPerHousehold] int NOT NULL,
    [Active] bit NOT NULL,
    CONSTRAINT [PK_ZoneTariffs] PRIMARY KEY ([ZoneCode])
);
GO


CREATE TABLE [permits].[Cases] (
    [Id] int NOT NULL IDENTITY,
    [Reference] nvarchar(20) NOT NULL,
    [Kind] int NOT NULL,
    [Status] int NOT NULL,
    [Channel] int NOT NULL,
    [ApplicantId] int NOT NULL,
    [Uprn] nvarchar(12) NULL,
    [SiteAddress] nvarchar(200) NULL,
    [ParcelId] nvarchar(20) NULL,
    [Easting] float NULL,
    [Northing] float NULL,
    [Ward] nvarchar(50) NULL,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(max) NULL,
    [Building_WorkType] int NULL,
    [Building_EstimatedCost] decimal(14,2) NULL,
    [Building_GrossFloorAreaM2] float NULL,
    [Building_FootprintM2] float NULL,
    [Building_HeightM] float NULL,
    [Building_Storeys] int NULL,
    [Building_UseClass] nvarchar(20) NULL,
    [Building_FootprintWkt] nvarchar(max) NULL,
    [Building_ListedBuilding] bit NULL,
    [Building_AgentName] nvarchar(120) NULL,
    [Building_AgentEmail] nvarchar(200) NULL,
    [Business_Category] int NULL,
    [Business_TradingName] nvarchar(120) NULL,
    [Business_OpeningHours] nvarchar(max) NULL,
    [Business_Capacity] int NULL,
    [Business_ResponsiblePerson] nvarchar(max) NULL,
    [Business_Seasonal] bit NULL,
    [Business_UseClass] nvarchar(20) NULL,
    [Parking_PermitType] int NULL,
    [Parking_Registration] nvarchar(10) NULL,
    [Parking_EmissionBand] int NULL,
    [Parking_ZoneCode] nvarchar(4) NULL,
    [Parking_PermitNumberInHousehold] int NULL,
    [Parking_DurationMonths] int NULL,
    [Parking_PermitSerial] nvarchar(20) NULL,
    [AssignedOfficer] nvarchar(100) NULL,
    [Team] nvarchar(50) NULL,
    [Priority] int NOT NULL,
    [CreatedUtc] datetime2 NOT NULL,
    [SubmittedUtc] datetime2 NULL,
    [TargetDecisionDate] datetime2 NULL,
    [DecidedUtc] datetime2 NULL,
    [IssuedUtc] datetime2 NULL,
    [ValidFrom] date NULL,
    [ValidTo] date NULL,
    [Outcome] int NULL,
    [DecisionNotes] nvarchar(max) NULL,
    [DecidedBy] nvarchar(100) NULL,
    [RenewalOfCaseId] int NULL,
    [RenewalReminderCount] int NOT NULL,
    [SpatialCheckJson] nvarchar(max) NULL,
    [SpatialCheckedUtc] datetime2 NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Cases] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Cases_Applicants_ApplicantId] FOREIGN KEY ([ApplicantId]) REFERENCES [permits].[Applicants] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [permits].[CaseDocuments] (
    [Id] int NOT NULL IDENTITY,
    [CaseId] int NOT NULL,
    [Kind] int NOT NULL,
    [FileName] nvarchar(255) NOT NULL,
    [ContentType] nvarchar(max) NOT NULL,
    [Length] bigint NOT NULL,
    [Sha256] nvarchar(64) NOT NULL,
    [ArchiveKey] uniqueidentifier NOT NULL,
    [ArchivePath] nvarchar(max) NULL,
    [CreatedUtc] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NOT NULL,
    [VisibleToApplicant] bit NOT NULL,
    [RecordsSystemId] nvarchar(max) NULL,
    [RecordsFiledUtc] datetime2 NULL,
    [RecordsAttempts] int NOT NULL,
    [RecordsError] nvarchar(max) NULL,
    CONSTRAINT [PK_CaseDocuments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CaseDocuments_Cases_CaseId] FOREIGN KEY ([CaseId]) REFERENCES [permits].[Cases] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [permits].[CaseEvents] (
    [Id] bigint NOT NULL IDENTITY,
    [CaseId] int NOT NULL,
    [AtUtc] datetime2 NOT NULL,
    [Actor] nvarchar(max) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [FromStatus] int NULL,
    [ToStatus] int NULL,
    [Text] nvarchar(max) NULL,
    [VisibleToApplicant] bit NOT NULL,
    CONSTRAINT [PK_CaseEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CaseEvents_Cases_CaseId] FOREIGN KEY ([CaseId]) REFERENCES [permits].[Cases] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [permits].[Conditions] (
    [Id] int NOT NULL IDENTITY,
    [CaseId] int NOT NULL,
    [Code] nvarchar(max) NOT NULL,
    [Text] nvarchar(max) NOT NULL,
    [Order] int NOT NULL,
    CONSTRAINT [PK_Conditions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Conditions_Cases_CaseId] FOREIGN KEY ([CaseId]) REFERENCES [permits].[Cases] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [permits].[Consultations] (
    [Id] int NOT NULL IDENTITY,
    [CaseId] int NOT NULL,
    [Consultee] nvarchar(max) NOT NULL,
    [Reason] nvarchar(max) NOT NULL,
    [SentUtc] datetime2 NOT NULL,
    [DueDate] date NOT NULL,
    [RespondedUtc] datetime2 NULL,
    [Response] nvarchar(max) NULL,
    [Objection] bit NULL,
    CONSTRAINT [PK_Consultations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Consultations_Cases_CaseId] FOREIGN KEY ([CaseId]) REFERENCES [permits].[Cases] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [permits].[FeeLines] (
    [Id] int NOT NULL IDENTITY,
    [CaseId] int NOT NULL,
    [Code] nvarchar(10) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Amount] decimal(12,2) NOT NULL,
    [VatRate] decimal(5,4) NOT NULL,
    [Waived] bit NOT NULL,
    [WaivedBy] nvarchar(max) NULL,
    [WaiverReason] nvarchar(max) NULL,
    [CreatedUtc] datetime2 NOT NULL,
    [InvoiceNumber] nvarchar(max) NULL,
    CONSTRAINT [PK_FeeLines] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FeeLines_Cases_CaseId] FOREIGN KEY ([CaseId]) REFERENCES [permits].[Cases] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [permits].[Inspections] (
    [Id] int NOT NULL IDENTITY,
    [CaseId] int NOT NULL,
    [Stage] nvarchar(max) NOT NULL,
    [Status] int NOT NULL,
    [ScheduledDate] date NULL,
    [Slot] nvarchar(max) NULL,
    [Inspector] nvarchar(450) NULL,
    [RouteOrder] int NULL,
    [StartedUtc] datetime2 NULL,
    [CompletedUtc] datetime2 NULL,
    [ResultNotes] nvarchar(max) NULL,
    [ChecklistJson] nvarchar(max) NULL,
    [Latitude] float NULL,
    [Longitude] float NULL,
    [ClientMutationId] nvarchar(450) NULL,
    CONSTRAINT [PK_Inspections] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Inspections_Cases_CaseId] FOREIGN KEY ([CaseId]) REFERENCES [permits].[Cases] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [permits].[Payments] (
    [Id] int NOT NULL IDENTITY,
    [CaseId] int NOT NULL,
    [ProviderReference] nvarchar(64) NOT NULL,
    [SessionId] nvarchar(64) NULL,
    [Amount] decimal(12,2) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [Status] int NOT NULL,
    [CardBrand] nvarchar(max) NULL,
    [Last4] nvarchar(max) NULL,
    [CreatedUtc] datetime2 NOT NULL,
    [SettledUtc] datetime2 NULL,
    [SettlementBatch] nvarchar(max) NULL,
    [FailureReason] nvarchar(max) NULL,
    CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Payments_Cases_CaseId] FOREIGN KEY ([CaseId]) REFERENCES [permits].[Cases] ([Id]) ON DELETE CASCADE
);
GO


CREATE UNIQUE INDEX [IX_Applicants_EidSubject] ON [permits].[Applicants] ([EidSubject]) WHERE [EidSubject] IS NOT NULL;
GO


CREATE UNIQUE INDEX [IX_CaseDocuments_ArchiveKey] ON [permits].[CaseDocuments] ([ArchiveKey]);
GO


CREATE INDEX [IX_CaseDocuments_CaseId] ON [permits].[CaseDocuments] ([CaseId]);
GO


CREATE INDEX [IX_CaseDocuments_RecordsFiledUtc] ON [permits].[CaseDocuments] ([RecordsFiledUtc]);
GO


CREATE INDEX [IX_CaseEvents_CaseId_AtUtc] ON [permits].[CaseEvents] ([CaseId], [AtUtc]);
GO


CREATE INDEX [IX_Cases_ApplicantId] ON [permits].[Cases] ([ApplicantId]);
GO


CREATE UNIQUE INDEX [IX_Cases_Reference] ON [permits].[Cases] ([Reference]);
GO


CREATE INDEX [IX_Cases_Status_Kind] ON [permits].[Cases] ([Status], [Kind]);
GO


CREATE INDEX [IX_Cases_Uprn] ON [permits].[Cases] ([Uprn]);
GO


CREATE INDEX [IX_Cases_ValidTo] ON [permits].[Cases] ([ValidTo]);
GO


CREATE INDEX [IX_Conditions_CaseId] ON [permits].[Conditions] ([CaseId]);
GO


CREATE INDEX [IX_Consultations_CaseId] ON [permits].[Consultations] ([CaseId]);
GO


CREATE INDEX [IX_FeeLines_CaseId] ON [permits].[FeeLines] ([CaseId]);
GO


CREATE INDEX [IX_Inspections_CaseId] ON [permits].[Inspections] ([CaseId]);
GO


CREATE UNIQUE INDEX [IX_Inspections_ClientMutationId] ON [permits].[Inspections] ([ClientMutationId]) WHERE [ClientMutationId] IS NOT NULL;
GO


CREATE INDEX [IX_Inspections_Inspector_ScheduledDate] ON [permits].[Inspections] ([Inspector], [ScheduledDate]);
GO


CREATE UNIQUE INDEX [IX_IntakeItems_SourceFile] ON [permits].[IntakeItems] ([SourceFile]);
GO


CREATE INDEX [IX_Outbox_ProcessedUtc_Id] ON [permits].[Outbox] ([ProcessedUtc], [Id]);
GO


CREATE UNIQUE INDEX [IX_PaymentNotifications_EventId] ON [permits].[PaymentNotifications] ([EventId]);
GO


CREATE INDEX [IX_Payments_CaseId] ON [permits].[Payments] ([CaseId]);
GO


CREATE UNIQUE INDEX [IX_Payments_ProviderReference] ON [permits].[Payments] ([ProviderReference]);
GO


