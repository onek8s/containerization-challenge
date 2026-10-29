-- Batch Release schema. Run once against an empty database.
-- sqlcmd -S release-sql.corp.local -E -d BatchRelease -i 01-schema.sql
:on error exit
SET NOCOUNT ON;
IF OBJECT_ID(N'br.Batch', N'U') IS NOT NULL
BEGIN
    PRINT 'Schema br already installed.';
    SET NOEXEC ON;
END
GO

IF SCHEMA_ID(N'br') IS NULL EXEC(N'CREATE SCHEMA [br];');
GO

CREATE TABLE [br].[ApiClient] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [KeyHash] nchar(64) NOT NULL,
    [KeyPrefix] nvarchar(8) NOT NULL,
    [CanSubmit] bit NOT NULL,
    [CanRead] bit NOT NULL,
    [SiteCode] nvarchar(6) NULL,
    [Active] bit NOT NULL,
    [LastUsedUtc] datetime2 NULL,
    CONSTRAINT [PK_ApiClient] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [br].[ProductFamily] (
    [Code] nvarchar(20) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [PolicySddl] nvarchar(4000) NOT NULL,
    [PolicyChangedUtc] datetime2 NOT NULL,
    [PolicyChangedBy] nvarchar(128) NOT NULL,
    CONSTRAINT [PK_ProductFamily] PRIMARY KEY ([Code])
);
GO

CREATE TABLE [br].[ReleaseCertificate] (
    [Id] int NOT NULL IDENTITY,
    [CertificateNumber] nvarchar(20) NOT NULL,
    [BatchId] int NOT NULL,
    [DecisionId] int NOT NULL,
    [SignerThumbprint] nchar(40) NOT NULL,
    [VaultPath] nvarchar(400) NOT NULL,
    [Sha256] nchar(64) NOT NULL,
    [IssuedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ReleaseCertificate] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [br].[Signer] (
    [Id] int NOT NULL IDENTITY,
    [AccountName] nvarchar(128) NOT NULL,
    [Sid] nvarchar(184) NOT NULL,
    [DisplayName] nvarchar(128) NOT NULL,
    [KeyName] nvarchar(128) NOT NULL,
    [KeyProvider] nvarchar(128) NOT NULL,
    [HardwareKey] bit NOT NULL,
    [Thumbprint] nchar(40) NOT NULL,
    [CertificateDer] varbinary(max) NOT NULL,
    [ValidFromUtc] datetime2 NOT NULL,
    [ValidToUtc] datetime2 NOT NULL,
    [EnrolledUtc] datetime2 NOT NULL,
    [EnrolledBy] nvarchar(128) NOT NULL,
    [Active] bit NOT NULL,
    CONSTRAINT [PK_Signer] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [br].[Product] (
    [Code] nvarchar(20) NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [FamilyCode] nvarchar(20) NOT NULL,
    [Strength] nvarchar(50) NOT NULL,
    [DosageForm] nvarchar(50) NOT NULL,
    [ShelfLifeMonths] int NOT NULL,
    [MinimumRemainingMonths] int NOT NULL,
    [Active] bit NOT NULL,
    CONSTRAINT [PK_Product] PRIMARY KEY ([Code]),
    CONSTRAINT [FK_Product_ProductFamily_FamilyCode] FOREIGN KEY ([FamilyCode]) REFERENCES [br].[ProductFamily] ([Code]) ON DELETE CASCADE
);
GO

CREATE TABLE [br].[Batch] (
    [Id] int NOT NULL IDENTITY,
    [BatchNumber] nvarchar(12) NOT NULL,
    [ProductCode] nvarchar(20) NOT NULL,
    [SiteCode] nvarchar(6) NOT NULL,
    [MesReference] nvarchar(40) NOT NULL,
    [ManufacturedOn] date NOT NULL,
    [ExpiresOn] date NOT NULL,
    [Quantity] decimal(18,3) NOT NULL,
    [Unit] nvarchar(10) NOT NULL,
    [Status] int NOT NULL,
    [ReceivedUtc] datetime2 NOT NULL,
    [DispositionUtc] datetime2 NULL,
    [CertificateNumber] nvarchar(20) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Batch] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Batch_Product_ProductCode] FOREIGN KEY ([ProductCode]) REFERENCES [br].[Product] ([Code]) ON DELETE CASCADE
);
GO

CREATE TABLE [br].[BatchDocument] (
    [Id] int NOT NULL IDENTITY,
    [BatchId] int NOT NULL,
    [Kind] nvarchar(40) NOT NULL,
    [FileName] nvarchar(200) NOT NULL,
    [ContentType] nvarchar(100) NOT NULL,
    [Length] bigint NOT NULL,
    [Sha256] nchar(64) NOT NULL,
    [VaultPath] nvarchar(400) NOT NULL,
    [StoredUtc] datetime2 NOT NULL,
    [StoredBy] nvarchar(128) NOT NULL,
    CONSTRAINT [PK_BatchDocument] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BatchDocument_Batch_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [br].[Batch] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [br].[Decision] (
    [Id] int NOT NULL IDENTITY,
    [BatchId] int NOT NULL,
    [Kind] int NOT NULL,
    [SignerAccount] nvarchar(128) NOT NULL,
    [SignerSid] nvarchar(184) NOT NULL,
    [SignerName] nvarchar(128) NOT NULL,
    [Meaning] nvarchar(400) NOT NULL,
    [Comment] nvarchar(2000) NULL,
    [SignedUtc] datetime2 NOT NULL,
    [SignerId] int NULL,
    [CertificateNumber] nvarchar(20) NULL,
    [SignatureSha256] nchar(64) NULL,
    CONSTRAINT [PK_Decision] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Decision_Batch_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [br].[Batch] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [br].[Deviation] (
    [Id] int NOT NULL IDENTITY,
    [BatchId] int NOT NULL,
    [Number] nvarchar(20) NOT NULL,
    [Title] nvarchar(300) NOT NULL,
    [Classification] int NOT NULL,
    [Status] int NOT NULL,
    [Impact] int NOT NULL,
    [CapaNumber] nvarchar(20) NULL,
    CONSTRAINT [PK_Deviation] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Deviation_Batch_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [br].[Batch] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [br].[LabResult] (
    [Id] int NOT NULL IDENTITY,
    [BatchId] int NOT NULL,
    [TestCode] nvarchar(20) NOT NULL,
    [TestName] nvarchar(150) NOT NULL,
    [Specification] nvarchar(200) NOT NULL,
    [LowerLimit] decimal(18,6) NULL,
    [UpperLimit] decimal(18,6) NULL,
    [Value] decimal(18,6) NULL,
    [TextResult] nvarchar(200) NULL,
    [Unit] nvarchar(20) NOT NULL,
    [Outcome] int NOT NULL,
    [InvestigationNumber] nvarchar(20) NULL,
    [InvestigationClosed] bit NOT NULL,
    [ApprovedBy] nvarchar(64) NULL,
    CONSTRAINT [PK_LabResult] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LabResult_Batch_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [br].[Batch] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [br].[RecordStep] (
    [Id] int NOT NULL IDENTITY,
    [BatchId] int NOT NULL,
    [Sequence] int NOT NULL,
    [Operation] nvarchar(200) NOT NULL,
    [Status] int NOT NULL,
    [PerformedBy] nvarchar(64) NULL,
    [VerifiedBy] nvarchar(64) NULL,
    [CompletedUtc] datetime2 NULL,
    [Comment] nvarchar(1000) NULL,
    CONSTRAINT [PK_RecordStep] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RecordStep_Batch_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [br].[Batch] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_ApiClient_KeyPrefix] ON [br].[ApiClient] ([KeyPrefix]);
GO

CREATE UNIQUE INDEX [IX_Batch_BatchNumber] ON [br].[Batch] ([BatchNumber]);
GO

CREATE UNIQUE INDEX [IX_Batch_MesReference] ON [br].[Batch] ([MesReference]);
GO

CREATE INDEX [IX_Batch_ProductCode] ON [br].[Batch] ([ProductCode]);
GO

CREATE INDEX [IX_BatchDocument_BatchId] ON [br].[BatchDocument] ([BatchId]);
GO

CREATE INDEX [IX_Decision_BatchId] ON [br].[Decision] ([BatchId]);
GO

CREATE INDEX [IX_Deviation_BatchId] ON [br].[Deviation] ([BatchId]);
GO

CREATE INDEX [IX_LabResult_BatchId] ON [br].[LabResult] ([BatchId]);
GO

CREATE INDEX [IX_Product_FamilyCode] ON [br].[Product] ([FamilyCode]);
GO

CREATE INDEX [IX_RecordStep_BatchId] ON [br].[RecordStep] ([BatchId]);
GO

CREATE UNIQUE INDEX [IX_ReleaseCertificate_CertificateNumber] ON [br].[ReleaseCertificate] ([CertificateNumber]);
GO

CREATE INDEX [IX_Signer_Sid] ON [br].[Signer] ([Sid]);
GO

CREATE UNIQUE INDEX [IX_Signer_Thumbprint] ON [br].[Signer] ([Thumbprint]);
GO


-- Decisions and certificates are records under 21 CFR Part 11: no updates or deletes.
CREATE TRIGGER [br].[Decision_NoChange] ON [br].[Decision] AFTER UPDATE, DELETE
AS
BEGIN
    RAISERROR(N'Signed decisions cannot be changed or deleted.', 16, 1);
    ROLLBACK TRANSACTION;
END
GO

CREATE TRIGGER [br].[ReleaseCertificate_NoChange] ON [br].[ReleaseCertificate] AFTER UPDATE, DELETE
AS
BEGIN
    RAISERROR(N'Batch certificates cannot be changed or deleted.', 16, 1);
    ROLLBACK TRANSACTION;
END
GO

SET NOEXEC OFF;
GO
