-- Study Data database. Run on sql01 as a sysadmin:
--   sqlcmd -S sql01.corp.local -E -i 01-create-database.sql
-- Generated from the StudyData.Data model (schema edc).

IF DB_ID(N'StudyData') IS NULL
BEGIN
    CREATE DATABASE [StudyData];
    ALTER DATABASE [StudyData] SET RECOVERY FULL;
    ALTER DATABASE [StudyData] SET READ_COMMITTED_SNAPSHOT ON;
END
GO

USE [StudyData];
GO

IF SCHEMA_ID(N'edc') IS NULL EXEC(N'CREATE SCHEMA [edc];');
GO


CREATE TABLE [edc].[AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [edc].[AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [FullName] nvarchar(max) NOT NULL,
    [SiteRole] nvarchar(max) NOT NULL,
    [Active] bit NOT NULL,
    [TrainingCompletedAt] datetime2 NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [edc].[Audit] (
    [Id] bigint NOT NULL IDENTITY,
    [At] datetime2 NOT NULL,
    [User] nvarchar(max) NOT NULL,
    [EntityType] nvarchar(450) NOT NULL,
    [EntityKey] nvarchar(450) NOT NULL,
    [SubjectId] int NULL,
    [Action] nvarchar(max) NOT NULL,
    [OldValue] nvarchar(max) NULL,
    [NewValue] nvarchar(max) NULL,
    [Reason] nvarchar(max) NULL,
    CONSTRAINT [PK_Audit] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [edc].[Coding] (
    [Id] int NOT NULL IDENTITY,
    [SubjectId] int NOT NULL,
    [FormInstanceId] int NOT NULL,
    [FieldCode] nvarchar(450) NOT NULL,
    [Dictionary] nvarchar(max) NOT NULL,
    [Verbatim] nvarchar(max) NOT NULL,
    [State] nvarchar(16) NOT NULL,
    [Code] nvarchar(max) NULL,
    [Term] nvarchar(max) NULL,
    [Hierarchy] nvarchar(max) NULL,
    [DictionaryVersion] nvarchar(max) NULL,
    [Score] int NULL,
    [CodedBy] nvarchar(max) NULL,
    [CodedAt] datetime2 NULL,
    CONSTRAINT [PK_Coding] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [edc].[GeneratedDocuments] (
    [Id] int NOT NULL IDENTITY,
    [StudyId] int NOT NULL,
    [SubjectId] int NULL,
    [Kind] nvarchar(450) NOT NULL,
    [State] nvarchar(16) NOT NULL,
    [RequestedBy] nvarchar(max) NOT NULL,
    [RequestedAt] datetime2 NOT NULL,
    [CompletedAt] datetime2 NULL,
    [Path] nvarchar(max) NULL,
    [Sha256] nvarchar(max) NULL,
    [Error] nvarchar(max) NULL,
    [Parameters] nvarchar(max) NULL,
    CONSTRAINT [PK_GeneratedDocuments] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [edc].[LabFiles] (
    [Id] int NOT NULL IDENTITY,
    [FileName] nvarchar(max) NOT NULL,
    [Sha256] nvarchar(450) NOT NULL,
    [ImportedAt] datetime2 NOT NULL,
    [Rows] int NOT NULL,
    [Rejected] int NOT NULL,
    [Errors] nvarchar(max) NULL,
    CONSTRAINT [PK_LabFiles] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [edc].[LabResults] (
    [Id] int NOT NULL IDENTITY,
    [StudyId] int NOT NULL,
    [SubjectId] int NOT NULL,
    [VisitCode] nvarchar(max) NOT NULL,
    [Accession] nvarchar(450) NOT NULL,
    [CollectedAt] datetime2 NOT NULL,
    [TestCode] nvarchar(450) NOT NULL,
    [TestName] nvarchar(max) NOT NULL,
    [Value] nvarchar(max) NULL,
    [Unit] nvarchar(max) NULL,
    [LowerLimit] decimal(18,4) NULL,
    [UpperLimit] decimal(18,4) NULL,
    [Flag] nvarchar(16) NOT NULL,
    [Grade] int NULL,
    [SourceFile] nvarchar(max) NOT NULL,
    [ImportedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_LabResults] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [edc].[Notifications] (
    [Id] bigint NOT NULL IDENTITY,
    [At] datetime2 NOT NULL,
    [StudyId] int NOT NULL,
    [SiteId] int NULL,
    [Kind] nvarchar(max) NOT NULL,
    [Text] nvarchar(max) NOT NULL,
    [SubjectId] int NULL,
    [Delivered] bit NOT NULL,
    CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [edc].[Queries] (
    [Id] int NOT NULL IDENTITY,
    [SubjectId] int NOT NULL,
    [FormInstanceId] int NOT NULL,
    [FieldCode] nvarchar(max) NOT NULL,
    [Origin] nvarchar(16) NOT NULL,
    [EditCheckCode] nvarchar(max) NULL,
    [State] nvarchar(16) NOT NULL,
    [Text] nvarchar(max) NOT NULL,
    [OpenedAt] datetime2 NOT NULL,
    [OpenedBy] nvarchar(max) NOT NULL,
    [LastReminderAt] datetime2 NULL,
    [ReminderCount] int NOT NULL,
    CONSTRAINT [PK_Queries] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [edc].[RandomisationSlots] (
    [Id] int NOT NULL IDENTITY,
    [StudyId] int NOT NULL,
    [SiteNumber] nvarchar(450) NOT NULL,
    [Stratum] nvarchar(450) NOT NULL,
    [Sequence] int NOT NULL,
    [RandomisationNumber] nvarchar(max) NOT NULL,
    [ArmCode] nvarchar(max) NOT NULL,
    [SubjectId] int NULL,
    [UsedAt] datetime2 NULL,
    CONSTRAINT [PK_RandomisationSlots] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [edc].[SourceDocuments] (
    [Id] int NOT NULL IDENTITY,
    [SubjectId] int NOT NULL,
    [VisitId] int NULL,
    [Category] nvarchar(max) NOT NULL,
    [OriginalName] nvarchar(max) NOT NULL,
    [StoredPath] nvarchar(max) NOT NULL,
    [ContentType] nvarchar(max) NOT NULL,
    [Length] bigint NOT NULL,
    [Sha256] nvarchar(max) NOT NULL,
    [Redacted] bit NOT NULL,
    [UploadedAt] datetime2 NOT NULL,
    [UploadedBy] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_SourceDocuments] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [edc].[SponsorClients] (
    [Id] int NOT NULL IDENTITY,
    [ClientId] nvarchar(450) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Studies] nvarchar(max) NOT NULL,
    [MayUnblind] bit NOT NULL,
    [Active] bit NOT NULL,
    CONSTRAINT [PK_SponsorClients] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [edc].[Studies] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(32) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Sponsor] nvarchar(max) NOT NULL,
    [SponsorClientId] nvarchar(max) NOT NULL,
    [Phase] nvarchar(max) NOT NULL,
    [Status] nvarchar(16) NOT NULL,
    [DesignVersion] int NOT NULL,
    [SubjectNumberFormat] nvarchar(max) NOT NULL,
    [TargetEnrolment] int NOT NULL,
    [QueryReminderDays] int NOT NULL,
    [Blinded] bit NOT NULL,
    CONSTRAINT [PK_Studies] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [edc].[AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [edc].[AspNetRoles] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [edc].[AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [edc].[AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [edc].[AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [edc].[AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [edc].[AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [edc].[AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [edc].[AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [edc].[AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [edc].[AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [edc].[SiteAssignments] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [SiteId] int NOT NULL,
    [CanSign] bit NOT NULL,
    CONSTRAINT [PK_SiteAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SiteAssignments_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [edc].[AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [edc].[QueryMessages] (
    [Id] int NOT NULL IDENTITY,
    [QueryId] int NOT NULL,
    [At] datetime2 NOT NULL,
    [User] nvarchar(max) NOT NULL,
    [Role] nvarchar(max) NOT NULL,
    [Text] nvarchar(max) NOT NULL,
    [NewState] nvarchar(16) NOT NULL,
    CONSTRAINT [PK_QueryMessages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_QueryMessages_Queries_QueryId] FOREIGN KEY ([QueryId]) REFERENCES [edc].[Queries] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [edc].[Arms] (
    [Id] int NOT NULL IDENTITY,
    [StudyId] int NOT NULL,
    [Code] nvarchar(450) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Ratio] int NOT NULL,
    CONSTRAINT [PK_Arms] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Arms_Studies_StudyId] FOREIGN KEY ([StudyId]) REFERENCES [edc].[Studies] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [edc].[FormDefinitions] (
    [Id] int NOT NULL IDENTITY,
    [StudyId] int NOT NULL,
    [Code] nvarchar(450) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Order] int NOT NULL,
    [Repeating] bit NOT NULL,
    [WorkbookCode] nvarchar(max) NULL,
    [PrintTemplate] nvarchar(max) NULL,
    CONSTRAINT [PK_FormDefinitions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FormDefinitions_Studies_StudyId] FOREIGN KEY ([StudyId]) REFERENCES [edc].[Studies] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [edc].[Sites] (
    [Id] int NOT NULL IDENTITY,
    [StudyId] int NOT NULL,
    [Number] nvarchar(450) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Investigator] nvarchar(max) NOT NULL,
    [Country] nvarchar(max) NOT NULL,
    [TimeZone] nvarchar(max) NOT NULL,
    [Active] bit NOT NULL,
    [ContactEmail] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Sites] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Sites_Studies_StudyId] FOREIGN KEY ([StudyId]) REFERENCES [edc].[Studies] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [edc].[VisitDefinitions] (
    [Id] int NOT NULL IDENTITY,
    [StudyId] int NOT NULL,
    [Code] nvarchar(450) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Order] int NOT NULL,
    [PlannedDay] int NOT NULL,
    [WindowBefore] int NOT NULL,
    [WindowAfter] int NOT NULL,
    [IsAnchor] bit NOT NULL,
    [Unscheduled] bit NOT NULL,
    [FormCodes] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_VisitDefinitions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_VisitDefinitions_Studies_StudyId] FOREIGN KEY ([StudyId]) REFERENCES [edc].[Studies] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [edc].[Workbooks] (
    [Id] int NOT NULL IDENTITY,
    [StudyId] int NOT NULL,
    [Code] nvarchar(450) NOT NULL,
    [Version] int NOT NULL,
    [FileName] nvarchar(max) NOT NULL,
    [Sha256] nvarchar(max) NOT NULL,
    [ValidatedBy] nvarchar(max) NOT NULL,
    [ValidatedAt] datetime2 NOT NULL,
    [Active] bit NOT NULL,
    CONSTRAINT [PK_Workbooks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Workbooks_Studies_StudyId] FOREIGN KEY ([StudyId]) REFERENCES [edc].[Studies] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [edc].[EditChecks] (
    [Id] int NOT NULL IDENTITY,
    [FormDefinitionId] int NOT NULL,
    [Code] nvarchar(max) NOT NULL,
    [Expression] nvarchar(max) NOT NULL,
    [When] nvarchar(max) NULL,
    [Message] nvarchar(max) NOT NULL,
    [FieldCode] nvarchar(max) NOT NULL,
    [Severity] nvarchar(16) NOT NULL,
    [Active] bit NOT NULL,
    CONSTRAINT [PK_EditChecks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EditChecks_FormDefinitions_FormDefinitionId] FOREIGN KEY ([FormDefinitionId]) REFERENCES [edc].[FormDefinitions] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [edc].[FieldDefinitions] (
    [Id] int NOT NULL IDENTITY,
    [FormDefinitionId] int NOT NULL,
    [Code] nvarchar(max) NOT NULL,
    [Label] nvarchar(max) NOT NULL,
    [Type] nvarchar(16) NOT NULL,
    [Order] int NOT NULL,
    [Unit] nvarchar(max) NULL,
    [Required] bit NOT NULL,
    [Min] decimal(18,4) NULL,
    [Max] decimal(18,4) NULL,
    [Decimals] int NULL,
    [MaxLength] int NULL,
    [Choices] nvarchar(max) NULL,
    [WorkbookOutput] nvarchar(max) NULL,
    [Sdv] bit NOT NULL,
    [CodingDictionary] nvarchar(max) NULL,
    CONSTRAINT [PK_FieldDefinitions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FieldDefinitions_FormDefinitions_FormDefinitionId] FOREIGN KEY ([FormDefinitionId]) REFERENCES [edc].[FormDefinitions] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [edc].[Subjects] (
    [Id] int NOT NULL IDENTITY,
    [StudyId] int NOT NULL,
    [SiteId] int NOT NULL,
    [Number] nvarchar(450) NOT NULL,
    [Initials] nvarchar(max) NULL,
    [BirthYear] int NOT NULL,
    [Sex] nvarchar(max) NOT NULL,
    [ConsentDate] date NULL,
    [Status] nvarchar(16) NOT NULL,
    [ArmId] int NULL,
    [RandomisedAt] datetime2 NULL,
    [RandomisationNumber] nvarchar(max) NULL,
    [WithdrawalReason] nvarchar(max) NULL,
    CONSTRAINT [PK_Subjects] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Subjects_Arms_ArmId] FOREIGN KEY ([ArmId]) REFERENCES [edc].[Arms] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Subjects_Sites_SiteId] FOREIGN KEY ([SiteId]) REFERENCES [edc].[Sites] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Subjects_Studies_StudyId] FOREIGN KEY ([StudyId]) REFERENCES [edc].[Studies] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [edc].[WorkbookInputs] (
    [Id] int NOT NULL IDENTITY,
    [CalculationWorkbookId] int NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Source] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_WorkbookInputs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkbookInputs_Workbooks_CalculationWorkbookId] FOREIGN KEY ([CalculationWorkbookId]) REFERENCES [edc].[Workbooks] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [edc].[Visits] (
    [Id] int NOT NULL IDENTITY,
    [SubjectId] int NOT NULL,
    [VisitDefinitionId] int NOT NULL,
    [Repeat] int NOT NULL,
    [Date] date NULL,
    [Status] nvarchar(16) NOT NULL,
    [OutOfWindow] bit NOT NULL,
    CONSTRAINT [PK_Visits] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Visits_Subjects_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [edc].[Subjects] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Visits_VisitDefinitions_VisitDefinitionId] FOREIGN KEY ([VisitDefinitionId]) REFERENCES [edc].[VisitDefinitions] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [edc].[Forms] (
    [Id] int NOT NULL IDENTITY,
    [VisitId] int NOT NULL,
    [FormDefinitionId] int NOT NULL,
    [Row] int NOT NULL,
    [Status] nvarchar(16) NOT NULL,
    [LastSavedAt] datetime2 NULL,
    [LastSavedBy] nvarchar(max) NULL,
    [SignedAt] datetime2 NULL,
    [SignedBy] nvarchar(max) NULL,
    [DerivedWith] nvarchar(max) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Forms] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Forms_FormDefinitions_FormDefinitionId] FOREIGN KEY ([FormDefinitionId]) REFERENCES [edc].[FormDefinitions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Forms_Visits_VisitId] FOREIGN KEY ([VisitId]) REFERENCES [edc].[Visits] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [edc].[Values] (
    [Id] int NOT NULL IDENTITY,
    [FormInstanceId] int NOT NULL,
    [FieldCode] nvarchar(450) NOT NULL,
    [Value] nvarchar(2000) NULL,
    [Verified] bit NOT NULL,
    [VerifiedBy] nvarchar(max) NULL,
    [VerifiedAt] datetime2 NULL,
    CONSTRAINT [PK_Values] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Values_Forms_FormInstanceId] FOREIGN KEY ([FormInstanceId]) REFERENCES [edc].[Forms] ([Id]) ON DELETE CASCADE
);
GO


CREATE UNIQUE INDEX [IX_Arms_StudyId_Code] ON [edc].[Arms] ([StudyId], [Code]);
GO


CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [edc].[AspNetRoleClaims] ([RoleId]);
GO


CREATE UNIQUE INDEX [RoleNameIndex] ON [edc].[AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
GO


CREATE INDEX [IX_AspNetUserClaims_UserId] ON [edc].[AspNetUserClaims] ([UserId]);
GO


CREATE INDEX [IX_AspNetUserLogins_UserId] ON [edc].[AspNetUserLogins] ([UserId]);
GO


CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [edc].[AspNetUserRoles] ([RoleId]);
GO


CREATE INDEX [EmailIndex] ON [edc].[AspNetUsers] ([NormalizedEmail]);
GO


CREATE UNIQUE INDEX [UserNameIndex] ON [edc].[AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
GO


CREATE INDEX [IX_Audit_EntityType_EntityKey] ON [edc].[Audit] ([EntityType], [EntityKey]);
GO


CREATE INDEX [IX_Audit_SubjectId_At] ON [edc].[Audit] ([SubjectId], [At]);
GO


CREATE UNIQUE INDEX [IX_Coding_FormInstanceId_FieldCode] ON [edc].[Coding] ([FormInstanceId], [FieldCode]);
GO


CREATE INDEX [IX_Coding_State] ON [edc].[Coding] ([State]);
GO


CREATE INDEX [IX_EditChecks_FormDefinitionId] ON [edc].[EditChecks] ([FormDefinitionId]);
GO


CREATE INDEX [IX_FieldDefinitions_FormDefinitionId] ON [edc].[FieldDefinitions] ([FormDefinitionId]);
GO


CREATE UNIQUE INDEX [IX_FormDefinitions_StudyId_Code] ON [edc].[FormDefinitions] ([StudyId], [Code]);
GO


CREATE INDEX [IX_Forms_FormDefinitionId] ON [edc].[Forms] ([FormDefinitionId]);
GO


CREATE UNIQUE INDEX [IX_Forms_VisitId_FormDefinitionId_Row] ON [edc].[Forms] ([VisitId], [FormDefinitionId], [Row]);
GO


CREATE INDEX [IX_GeneratedDocuments_Kind_State] ON [edc].[GeneratedDocuments] ([Kind], [State]);
GO


CREATE UNIQUE INDEX [IX_LabFiles_Sha256] ON [edc].[LabFiles] ([Sha256]);
GO


CREATE UNIQUE INDEX [IX_LabResults_Accession_TestCode] ON [edc].[LabResults] ([Accession], [TestCode]);
GO


CREATE INDEX [IX_Notifications_Delivered_Id] ON [edc].[Notifications] ([Delivered], [Id]);
GO


CREATE INDEX [IX_Queries_SubjectId_State] ON [edc].[Queries] ([SubjectId], [State]);
GO


CREATE INDEX [IX_QueryMessages_QueryId] ON [edc].[QueryMessages] ([QueryId]);
GO


CREATE UNIQUE INDEX [IX_RandomisationSlots_StudyId_SiteNumber_Stratum_Sequence] ON [edc].[RandomisationSlots] ([StudyId], [SiteNumber], [Stratum], [Sequence]);
GO


CREATE UNIQUE INDEX [IX_SiteAssignments_UserId_SiteId] ON [edc].[SiteAssignments] ([UserId], [SiteId]);
GO


CREATE UNIQUE INDEX [IX_Sites_StudyId_Number] ON [edc].[Sites] ([StudyId], [Number]);
GO


CREATE UNIQUE INDEX [IX_SponsorClients_ClientId] ON [edc].[SponsorClients] ([ClientId]);
GO


CREATE UNIQUE INDEX [IX_Studies_Code] ON [edc].[Studies] ([Code]);
GO


CREATE INDEX [IX_Subjects_ArmId] ON [edc].[Subjects] ([ArmId]);
GO


CREATE INDEX [IX_Subjects_SiteId] ON [edc].[Subjects] ([SiteId]);
GO


CREATE UNIQUE INDEX [IX_Subjects_StudyId_Number] ON [edc].[Subjects] ([StudyId], [Number]);
GO


CREATE UNIQUE INDEX [IX_Values_FormInstanceId_FieldCode] ON [edc].[Values] ([FormInstanceId], [FieldCode]);
GO


CREATE UNIQUE INDEX [IX_VisitDefinitions_StudyId_Code] ON [edc].[VisitDefinitions] ([StudyId], [Code]);
GO


CREATE UNIQUE INDEX [IX_Visits_SubjectId_VisitDefinitionId_Repeat] ON [edc].[Visits] ([SubjectId], [VisitDefinitionId], [Repeat]);
GO


CREATE INDEX [IX_Visits_VisitDefinitionId] ON [edc].[Visits] ([VisitDefinitionId]);
GO


CREATE INDEX [IX_WorkbookInputs_CalculationWorkbookId] ON [edc].[WorkbookInputs] ([CalculationWorkbookId]);
GO


CREATE UNIQUE INDEX [IX_Workbooks_StudyId_Code_Version] ON [edc].[Workbooks] ([StudyId], [Code], [Version]);
GO


