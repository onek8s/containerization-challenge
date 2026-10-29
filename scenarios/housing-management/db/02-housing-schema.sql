/*
    Fabrikam Housing - housing schema (tables used by all applications)
    sqlcmd -S sql01.corp.local -E -d FabrikamHousing -i 02-housing-schema.sql
*/
SET XACT_ABORT ON;
GO

IF SCHEMA_ID(N'housing') IS NULL EXEC(N'CREATE SCHEMA [housing];');
GO


CREATE TABLE [housing].[AuditEntries] (
    [Id] bigint NOT NULL IDENTITY,
    [At] datetime2 NOT NULL,
    [Actor] nvarchar(100) NULL,
    [Application] nvarchar(40) NULL,
    [EntityType] nvarchar(60) NULL,
    [EntityKey] nvarchar(60) NULL,
    [Action] nvarchar(60) NULL,
    [Details] nvarchar(max) NULL,
    CONSTRAINT [PK_AuditEntries] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [housing].[BankHolidays] (
    [Date] datetime2 NOT NULL,
    [Name] nvarchar(max) NULL,
    CONSTRAINT [PK_BankHolidays] PRIMARY KEY ([Date])
);
GO


CREATE TABLE [housing].[Communications] (
    [Id] int NOT NULL IDENTITY,
    [TenancyId] int NULL,
    [ContractorId] int NULL,
    [Channel] int NOT NULL,
    [TemplateCode] nvarchar(40) NULL,
    [Recipient] nvarchar(200) NULL,
    [Subject] nvarchar(200) NULL,
    [Body] nvarchar(max) NULL,
    [Status] int NOT NULL,
    [ProviderReference] nvarchar(60) NULL,
    [Error] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [SentAt] datetime2 NULL,
    [DocumentId] int NULL,
    CONSTRAINT [PK_Communications] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [housing].[ComplianceReminders] (
    [Id] int NOT NULL IDENTITY,
    [PropertyId] int NOT NULL,
    [Type] int NOT NULL,
    [DueDate] datetime2 NOT NULL,
    [Stage] int NOT NULL,
    [SentAt] datetime2 NOT NULL,
    [Channel] nvarchar(max) NULL,
    CONSTRAINT [PK_ComplianceReminders] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [housing].[Contractors] (
    [Id] int NOT NULL IDENTITY,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] nvarchar(max) NULL,
    [Code] nvarchar(10) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Trades] nvarchar(max) NULL,
    [Patches] nvarchar(max) NULL,
    [OutOfHours] bit NOT NULL,
    [GasSafeRegistered] bit NOT NULL,
    [GasSafeNumber] nvarchar(max) NULL,
    [WeeklyCapacity] int NOT NULL,
    [ContactEmail] nvarchar(max) NULL,
    [ContactPhone] nvarchar(max) NULL,
    [RemittanceEmail] nvarchar(max) NULL,
    [VatRegistered] bit NOT NULL,
    [VatNumber] nvarchar(max) NULL,
    [BankSortCode] nvarchar(8) NULL,
    [BankAccountNumber] nvarchar(8) NULL,
    [BankAccountName] nvarchar(max) NULL,
    [ApiClientId] nvarchar(60) NULL,
    [ApiClientSecretHash] nvarchar(max) NULL,
    [ApiSecretIssuedAt] datetime2 NULL,
    [Active] bit NOT NULL,
    CONSTRAINT [PK_Contractors] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [housing].[Counters] (
    [Name] nvarchar(40) NOT NULL,
    [Value] int NOT NULL,
    CONSTRAINT [PK_Counters] PRIMARY KEY ([Name])
);
GO


CREATE TABLE [housing].[Documents] (
    [Id] int NOT NULL IDENTITY,
    [DocumentKey] uniqueidentifier NOT NULL,
    [Category] int NOT NULL,
    [Title] nvarchar(200) NULL,
    [FileName] nvarchar(260) NULL,
    [ContentType] nvarchar(100) NULL,
    [SizeBytes] bigint NOT NULL,
    [Sha256] nvarchar(64) NULL,
    [StoragePath] nvarchar(400) NULL,
    [ThumbnailPath] nvarchar(400) NULL,
    [PropertyId] int NULL,
    [TenancyId] int NULL,
    [WorkOrderId] int NULL,
    [ContractorId] int NULL,
    [VisibleToTenant] bit NOT NULL,
    [UploadedAt] datetime2 NOT NULL,
    [UploadedBy] nvarchar(max) NULL,
    [RetainUntil] datetime2 NULL,
    [IndexedAt] datetime2 NULL,
    [Deleted] bit NOT NULL,
    CONSTRAINT [PK_Documents] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [housing].[Estates] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(10) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [PatchCode] nvarchar(10) NULL,
    [HasCommunalAreas] bit NOT NULL,
    [HasLift] bit NOT NULL,
    CONSTRAINT [PK_Estates] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [housing].[HousingBenefitImports] (
    [Id] int NOT NULL IDENTITY,
    [FileName] nvarchar(260) NULL,
    [ImportedAt] datetime2 NOT NULL,
    [Lines] int NOT NULL,
    [Matched] int NOT NULL,
    [Total] decimal(12,2) NOT NULL,
    [Unmatched] nvarchar(max) NULL,
    CONSTRAINT [PK_HousingBenefitImports] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [housing].[JournalBatches] (
    [Id] int NOT NULL IDENTITY,
    [CreatedAt] datetime2 NOT NULL,
    [PeriodEnd] datetime2 NOT NULL,
    [FileName] nvarchar(max) NULL,
    [LineCount] int NOT NULL,
    [TotalDebit] decimal(12,2) NOT NULL,
    [Status] nvarchar(max) NULL,
    CONSTRAINT [PK_JournalBatches] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [housing].[Patches] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(10) NOT NULL,
    [Name] nvarchar(100) NULL,
    [OfficerLogin] nvarchar(100) NULL,
    [OfficerName] nvarchar(100) NULL,
    [OfficerEmail] nvarchar(200) NULL,
    CONSTRAINT [PK_Patches] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [housing].[PaymentRuns] (
    [Id] int NOT NULL IDENTITY,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [ProcessingDate] datetime2 NOT NULL,
    [FileName] nvarchar(max) NULL,
    [PaymentCount] int NOT NULL,
    [Total] decimal(12,2) NOT NULL,
    [SubmittedAt] datetime2 NULL,
    [Status] nvarchar(max) NULL,
    CONSTRAINT [PK_PaymentRuns] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [housing].[ProcessedWebhooks] (
    [EventId] nvarchar(60) NOT NULL,
    [ReceivedAt] datetime2 NOT NULL,
    [ResourceType] nvarchar(max) NULL,
    [Action] nvarchar(max) NULL,
    CONSTRAINT [PK_ProcessedWebhooks] PRIMARY KEY ([EventId])
);
GO


CREATE TABLE [housing].[ScheduleOfRates] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(10) NOT NULL,
    [Description] nvarchar(200) NULL,
    [Trade] int NOT NULL,
    [DefaultPriority] int NOT NULL,
    [UnitPrice] decimal(12,2) NOT NULL,
    [Unit] nvarchar(10) NULL,
    [Active] bit NOT NULL,
    CONSTRAINT [PK_ScheduleOfRates] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [housing].[Properties] (
    [Id] int NOT NULL IDENTITY,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] nvarchar(max) NULL,
    [Uprn] bigint NOT NULL,
    [Reference] nvarchar(12) NOT NULL,
    [EstateId] int NOT NULL,
    [AddressLine1] nvarchar(100) NOT NULL,
    [AddressLine2] nvarchar(100) NULL,
    [Town] nvarchar(60) NULL,
    [Postcode] nvarchar(8) NOT NULL,
    [Latitude] float NULL,
    [Longitude] float NULL,
    [PatchCode] nvarchar(10) NULL,
    [Type] int NOT NULL,
    [Bedrooms] int NOT NULL,
    [YearBuilt] int NULL,
    [Status] int NOT NULL,
    [HasGasSupply] bit NOT NULL,
    [HasSmokeAlarms] bit NOT NULL,
    [AsbestosPresent] bit NOT NULL,
    [WeeklyNetRent] decimal(12,2) NOT NULL,
    [WeeklyServiceCharge] decimal(12,2) NOT NULL,
    [WeeklyWaterCharge] decimal(12,2) NOT NULL,
    CONSTRAINT [PK_Properties] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Properties_Estates_EstateId] FOREIGN KEY ([EstateId]) REFERENCES [housing].[Estates] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [housing].[Certificates] (
    [Id] int NOT NULL IDENTITY,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] nvarchar(max) NULL,
    [PropertyId] int NULL,
    [EstateId] int NULL,
    [Type] int NOT NULL,
    [CertificateNumber] nvarchar(40) NULL,
    [InspectedOn] datetime2 NOT NULL,
    [ExpiresOn] datetime2 NOT NULL,
    [Outcome] int NOT NULL,
    [EngineerName] nvarchar(100) NULL,
    [EngineerRegistration] nvarchar(20) NULL,
    [ContractorId] int NULL,
    [DocumentId] int NULL,
    [Observations] nvarchar(max) NULL,
    [RemedialWorkOrderId] int NULL,
    [Superseded] bit NOT NULL,
    CONSTRAINT [PK_Certificates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Certificates_Properties_PropertyId] FOREIGN KEY ([PropertyId]) REFERENCES [housing].[Properties] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [housing].[Tenancies] (
    [Id] int NOT NULL IDENTITY,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] nvarchar(max) NULL,
    [Reference] nvarchar(12) NOT NULL,
    [PropertyId] int NOT NULL,
    [Type] int NOT NULL,
    [Status] int NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NULL,
    [WeeklyCharge] decimal(12,2) NOT NULL,
    [HousingBenefitClaimed] bit NOT NULL,
    [HousingBenefitClaimRef] nvarchar(20) NULL,
    [UniversalCredit] bit NOT NULL,
    CONSTRAINT [PK_Tenancies] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Tenancies_Properties_PropertyId] FOREIGN KEY ([PropertyId]) REFERENCES [housing].[Properties] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [housing].[WorkOrders] (
    [Id] int NOT NULL IDENTITY,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] nvarchar(max) NULL,
    [Reference] nvarchar(12) NOT NULL,
    [PropertyId] int NOT NULL,
    [TenancyId] int NULL,
    [Channel] int NOT NULL,
    [ReportedBy] nvarchar(100) NULL,
    [Location] nvarchar(100) NULL,
    [Description] nvarchar(2000) NULL,
    [Trade] int NOT NULL,
    [Priority] int NOT NULL,
    [Status] int NOT NULL,
    [ReportedAt] datetime2 NOT NULL,
    [TargetDate] datetime2 NOT NULL,
    [ContractorId] int NULL,
    [AssignedAt] datetime2 NULL,
    [AcceptedAt] datetime2 NULL,
    [CompletedAt] datetime2 NULL,
    [CompletionNotes] nvarchar(max) NULL,
    [LabourHours] decimal(6,2) NULL,
    [Recharge] bit NOT NULL,
    [RechargeReason] nvarchar(max) NULL,
    [NoAccessCount] int NOT NULL,
    [SatisfactionScore] int NULL,
    [CancellationReason] nvarchar(max) NULL,
    [SlaBreachNotified] bit NOT NULL,
    CONSTRAINT [PK_WorkOrders] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrders_Contractors_ContractorId] FOREIGN KEY ([ContractorId]) REFERENCES [housing].[Contractors] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkOrders_Properties_PropertyId] FOREIGN KEY ([PropertyId]) REFERENCES [housing].[Properties] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [housing].[Arrangements] (
    [Id] int NOT NULL IDENTITY,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] nvarchar(max) NULL,
    [TenancyId] int NOT NULL,
    [WeeklyAmountTowardsArrears] decimal(12,2) NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NULL,
    [Status] int NOT NULL,
    [Notes] nvarchar(max) NULL,
    CONSTRAINT [PK_Arrangements] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Arrangements_Tenancies_TenancyId] FOREIGN KEY ([TenancyId]) REFERENCES [housing].[Tenancies] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [housing].[ArrearsCases] (
    [Id] int NOT NULL IDENTITY,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] nvarchar(max) NULL,
    [TenancyId] int NOT NULL,
    [Stage] int NOT NULL,
    [BalanceAtStage] decimal(12,2) NOT NULL,
    [StageReachedOn] datetime2 NOT NULL,
    [NextReviewOn] datetime2 NULL,
    [OnHold] bit NOT NULL,
    [HoldReason] nvarchar(max) NULL,
    [ClosedOn] datetime2 NULL,
    [AssignedTo] nvarchar(max) NULL,
    CONSTRAINT [PK_ArrearsCases] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ArrearsCases_Tenancies_TenancyId] FOREIGN KEY ([TenancyId]) REFERENCES [housing].[Tenancies] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [housing].[Enquiries] (
    [Id] int NOT NULL IDENTITY,
    [TenancyId] int NOT NULL,
    [Topic] int NOT NULL,
    [Subject] nvarchar(200) NOT NULL,
    [Message] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [AssignedTo] nvarchar(max) NULL,
    [Response] nvarchar(max) NULL,
    [RespondedAt] datetime2 NULL,
    [RespondedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_Enquiries] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Enquiries_Tenancies_TenancyId] FOREIGN KEY ([TenancyId]) REFERENCES [housing].[Tenancies] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [housing].[Mandates] (
    [Id] int NOT NULL IDENTITY,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] nvarchar(max) NULL,
    [TenancyId] int NOT NULL,
    [ProviderCustomerId] nvarchar(40) NULL,
    [ProviderMandateId] nvarchar(40) NULL,
    [BankReference] nvarchar(18) NULL,
    [AccountHolderName] nvarchar(100) NULL,
    [AccountNumberEnding] nvarchar(4) NULL,
    [Status] int NOT NULL,
    [Frequency] int NOT NULL,
    [CollectionDay] int NOT NULL,
    [Amount] decimal(12,2) NOT NULL,
    [FirstCollectionDate] datetime2 NOT NULL,
    [NextCollectionDate] datetime2 NULL,
    [CancelledAt] datetime2 NULL,
    [CancellationReason] nvarchar(max) NULL,
    CONSTRAINT [PK_Mandates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Mandates_Tenancies_TenancyId] FOREIGN KEY ([TenancyId]) REFERENCES [housing].[Tenancies] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [housing].[RentTransactions] (
    [Id] int NOT NULL IDENTITY,
    [TenancyId] int NOT NULL,
    [PostedOn] datetime2 NOT NULL,
    [RentWeek] datetime2 NOT NULL,
    [Type] int NOT NULL,
    [Amount] decimal(12,2) NOT NULL,
    [Reference] nvarchar(40) NULL,
    [Description] nvarchar(200) NULL,
    [Source] nvarchar(30) NULL,
    [JournalBatchId] int NULL,
    CONSTRAINT [PK_RentTransactions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RentTransactions_Tenancies_TenancyId] FOREIGN KEY ([TenancyId]) REFERENCES [housing].[Tenancies] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [housing].[Tenants] (
    [Id] int NOT NULL IDENTITY,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] nvarchar(max) NULL,
    [TenancyId] int NOT NULL,
    [IsLeadTenant] bit NOT NULL,
    [Title] nvarchar(10) NULL,
    [FirstName] nvarchar(60) NOT NULL,
    [LastName] nvarchar(60) NOT NULL,
    [DateOfBirth] datetime2 NOT NULL,
    [Email] nvarchar(200) NULL,
    [MobilePhone] nvarchar(20) NULL,
    [ContactPreference] int NOT NULL,
    [NeedsLargePrint] bit NOT NULL,
    [Vulnerable] bit NOT NULL,
    [VulnerabilityNotes] nvarchar(max) NULL,
    [PortalSubjectId] nvarchar(100) NULL,
    CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Tenants_Tenancies_TenancyId] FOREIGN KEY ([TenancyId]) REFERENCES [housing].[Tenancies] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [housing].[Appointments] (
    [Id] int NOT NULL IDENTITY,
    [WorkOrderId] int NOT NULL,
    [Date] datetime2 NOT NULL,
    [Slot] int NOT NULL,
    [BookedBy] nvarchar(max) NULL,
    [BookedAt] datetime2 NOT NULL,
    [Cancelled] bit NOT NULL,
    [Outcome] nvarchar(max) NULL,
    CONSTRAINT [PK_Appointments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Appointments_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [housing].[WorkOrders] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [housing].[ContractorInvoices] (
    [Id] int NOT NULL IDENTITY,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] nvarchar(max) NULL,
    [ContractorId] int NOT NULL,
    [InvoiceNumber] nvarchar(30) NOT NULL,
    [InvoiceDate] datetime2 NOT NULL,
    [WorkOrderId] int NOT NULL,
    [NetAmount] decimal(12,2) NOT NULL,
    [VatAmount] decimal(12,2) NOT NULL,
    [Status] int NOT NULL,
    [QueryReason] nvarchar(max) NULL,
    [ApprovedBy] nvarchar(max) NULL,
    [ApprovedAt] datetime2 NULL,
    [PaymentRunId] int NULL,
    [JournalBatchId] int NULL,
    CONSTRAINT [PK_ContractorInvoices] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ContractorInvoices_Contractors_ContractorId] FOREIGN KEY ([ContractorId]) REFERENCES [housing].[Contractors] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ContractorInvoices_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [housing].[WorkOrders] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [housing].[WorkOrderEvents] (
    [Id] int NOT NULL IDENTITY,
    [WorkOrderId] int NOT NULL,
    [At] datetime2 NOT NULL,
    [Actor] nvarchar(100) NULL,
    [NewStatus] int NULL,
    [Text] nvarchar(max) NULL,
    CONSTRAINT [PK_WorkOrderEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderEvents_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [housing].[WorkOrders] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [housing].[WorkOrderLines] (
    [Id] int NOT NULL IDENTITY,
    [WorkOrderId] int NOT NULL,
    [SorCode] nvarchar(10) NULL,
    [Description] nvarchar(200) NULL,
    [Quantity] decimal(9,2) NOT NULL,
    [UnitPrice] decimal(12,2) NOT NULL,
    [AddedByContractor] bit NOT NULL,
    CONSTRAINT [PK_WorkOrderLines] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkOrderLines_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [housing].[WorkOrders] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [housing].[ArrearsCaseNotes] (
    [Id] int NOT NULL IDENTITY,
    [ArrearsCaseId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [Text] nvarchar(max) NULL,
    CONSTRAINT [PK_ArrearsCaseNotes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ArrearsCaseNotes_ArrearsCases_ArrearsCaseId] FOREIGN KEY ([ArrearsCaseId]) REFERENCES [housing].[ArrearsCases] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [housing].[DirectDebitPayments] (
    [Id] int NOT NULL IDENTITY,
    [MandateId] int NOT NULL,
    [ProviderPaymentId] nvarchar(40) NULL,
    [ChargeDate] datetime2 NOT NULL,
    [Amount] decimal(12,2) NOT NULL,
    [Status] int NOT NULL,
    [FailureReason] nvarchar(200) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [RentTransactionId] int NULL,
    CONSTRAINT [PK_DirectDebitPayments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DirectDebitPayments_Mandates_MandateId] FOREIGN KEY ([MandateId]) REFERENCES [housing].[Mandates] ([Id]) ON DELETE CASCADE
);
GO


CREATE INDEX [IX_Appointments_WorkOrderId] ON [housing].[Appointments] ([WorkOrderId]);
GO


CREATE INDEX [IX_Arrangements_TenancyId] ON [housing].[Arrangements] ([TenancyId]);
GO


CREATE INDEX [IX_ArrearsCaseNotes_ArrearsCaseId] ON [housing].[ArrearsCaseNotes] ([ArrearsCaseId]);
GO


CREATE INDEX [IX_ArrearsCases_TenancyId] ON [housing].[ArrearsCases] ([TenancyId]);
GO


CREATE INDEX [IX_Certificates_PropertyId_Type] ON [housing].[Certificates] ([PropertyId], [Type]);
GO


CREATE INDEX [IX_ContractorInvoices_WorkOrderId] ON [housing].[ContractorInvoices] ([WorkOrderId]);
GO


CREATE UNIQUE INDEX [IX_ContractorInvoices_ContractorId_InvoiceNumber] ON [housing].[ContractorInvoices] ([ContractorId], [InvoiceNumber]);
GO


CREATE INDEX [IX_Contractors_ApiClientId] ON [housing].[Contractors] ([ApiClientId]);
GO


CREATE UNIQUE INDEX [IX_Contractors_Code] ON [housing].[Contractors] ([Code]);
GO


CREATE INDEX [IX_DirectDebitPayments_MandateId] ON [housing].[DirectDebitPayments] ([MandateId]);
GO


CREATE INDEX [IX_DirectDebitPayments_ProviderPaymentId] ON [housing].[DirectDebitPayments] ([ProviderPaymentId]);
GO


CREATE UNIQUE INDEX [IX_Documents_DocumentKey] ON [housing].[Documents] ([DocumentKey]);
GO


CREATE INDEX [IX_Documents_PropertyId] ON [housing].[Documents] ([PropertyId]);
GO


CREATE INDEX [IX_Documents_TenancyId] ON [housing].[Documents] ([TenancyId]);
GO


CREATE INDEX [IX_Enquiries_TenancyId] ON [housing].[Enquiries] ([TenancyId]);
GO


CREATE UNIQUE INDEX [IX_Estates_Code] ON [housing].[Estates] ([Code]);
GO


CREATE INDEX [IX_Mandates_ProviderMandateId] ON [housing].[Mandates] ([ProviderMandateId]);
GO


CREATE INDEX [IX_Mandates_TenancyId] ON [housing].[Mandates] ([TenancyId]);
GO


CREATE UNIQUE INDEX [IX_Patches_Code] ON [housing].[Patches] ([Code]);
GO


CREATE INDEX [IX_Properties_EstateId] ON [housing].[Properties] ([EstateId]);
GO


CREATE INDEX [IX_Properties_Postcode] ON [housing].[Properties] ([Postcode]);
GO


CREATE UNIQUE INDEX [IX_Properties_Reference] ON [housing].[Properties] ([Reference]);
GO


CREATE UNIQUE INDEX [IX_Properties_Uprn] ON [housing].[Properties] ([Uprn]);
GO


CREATE INDEX [IX_RentTransactions_JournalBatchId] ON [housing].[RentTransactions] ([JournalBatchId]);
GO


CREATE INDEX [IX_RentTransactions_TenancyId_RentWeek] ON [housing].[RentTransactions] ([TenancyId], [RentWeek]);
GO


CREATE UNIQUE INDEX [IX_ScheduleOfRates_Code] ON [housing].[ScheduleOfRates] ([Code]);
GO


CREATE INDEX [IX_Tenancies_PropertyId] ON [housing].[Tenancies] ([PropertyId]);
GO


CREATE UNIQUE INDEX [IX_Tenancies_Reference] ON [housing].[Tenancies] ([Reference]);
GO


CREATE INDEX [IX_Tenants_PortalSubjectId] ON [housing].[Tenants] ([PortalSubjectId]);
GO


CREATE INDEX [IX_Tenants_TenancyId] ON [housing].[Tenants] ([TenancyId]);
GO


CREATE INDEX [IX_WorkOrderEvents_WorkOrderId] ON [housing].[WorkOrderEvents] ([WorkOrderId]);
GO


CREATE INDEX [IX_WorkOrderLines_WorkOrderId] ON [housing].[WorkOrderLines] ([WorkOrderId]);
GO


CREATE INDEX [IX_WorkOrders_ContractorId] ON [housing].[WorkOrders] ([ContractorId]);
GO


CREATE INDEX [IX_WorkOrders_PropertyId] ON [housing].[WorkOrders] ([PropertyId]);
GO


CREATE UNIQUE INDEX [IX_WorkOrders_Reference] ON [housing].[WorkOrders] ([Reference]);
GO


CREATE INDEX [IX_WorkOrders_Status_ContractorId] ON [housing].[WorkOrders] ([Status], [ContractorId]);
GO




CREATE TABLE [housing].[ErrorLog] (
    [Id] bigint NOT NULL IDENTITY,
    [Logged] datetime2 NOT NULL CONSTRAINT [DF_ErrorLog_Logged] DEFAULT (SYSDATETIME()),
    [Application] nvarchar(40) NOT NULL,
    [MachineName] nvarchar(60) NOT NULL,
    [Level] nvarchar(10) NOT NULL,
    [Logger] nvarchar(250) NULL,
    [Message] nvarchar(max) NULL,
    [Exception] nvarchar(max) NULL,
    [Url] nvarchar(400) NULL,
    [UserName] nvarchar(100) NULL,
    CONSTRAINT [PK_ErrorLog] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_ErrorLog_Logged] ON [housing].[ErrorLog] ([Logged]);
GO

/* Rent account balances for the finance team's Excel queries (read through the housing_reports role). */
CREATE VIEW [housing].[RentAccountBalances]
AS
SELECT t.Id AS TenancyId, t.Reference AS TenancyReference, p.Reference AS PropertyReference, p.PatchCode,
       SUM(r.Amount) AS Balance, MAX(CASE WHEN r.Amount < 0 THEN r.PostedOn END) AS LastPaymentOn
FROM housing.Tenancies t
JOIN housing.Properties p ON p.Id = t.PropertyId
LEFT JOIN housing.RentTransactions r ON r.TenancyId = t.Id
GROUP BY t.Id, t.Reference, p.Reference, p.PatchCode;
GO
