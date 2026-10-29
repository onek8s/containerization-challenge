-- Quoting database schema (HumongousQuoting). Run with sqlcmd -d HumongousQuoting.
-- Generated from QuotingDbContext; keep in step with Humongous.Quoting.Data.

CREATE TABLE [Agencies] (
    [Code] nvarchar(10) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [FcaNumber] nvarchar(10) NOT NULL,
    [CommissionRate] decimal(5,4) NOT NULL,
    [Active] bit NOT NULL,
    [DocumentEmail] nvarchar(200) NOT NULL,
    [CreatedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Agencies] PRIMARY KEY ([Code])
);
GO


CREATE TABLE [BordereauRuns] (
    [Id] uniqueidentifier NOT NULL,
    [Period] nvarchar(7) NOT NULL,
    [CreatedUtc] datetime2 NOT NULL,
    [Rows] int NOT NULL,
    [GrossPremium] decimal(18,2) NOT NULL,
    [Ipt] decimal(18,2) NOT NULL,
    [Commission] decimal(18,2) NOT NULL,
    [FileName] nvarchar(200) NOT NULL,
    [Content] varbinary(max) NULL,
    [Status] nvarchar(20) NOT NULL,
    [Error] nvarchar(2000) NULL,
    [DeliveredUtc] datetime2 NULL,
    CONSTRAINT [PK_BordereauRuns] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Documents] (
    [Id] uniqueidentifier NOT NULL,
    [DocumentType] nvarchar(30) NOT NULL,
    [QuoteId] uniqueidentifier NULL,
    [PolicyId] uniqueidentifier NULL,
    [TransactionId] uniqueidentifier NULL,
    [AgencyCode] nvarchar(10) NOT NULL,
    [FileName] nvarchar(200) NOT NULL,
    [ContentType] nvarchar(100) NOT NULL,
    [Content] varbinary(max) NOT NULL,
    [Sha256] nvarchar(64) NOT NULL,
    [CreatedUtc] datetime2 NOT NULL,
    [EmailedTo] nvarchar(200) NULL,
    [EmailedUtc] datetime2 NULL,
    CONSTRAINT [PK_Documents] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [NumberSequences] (
    [Name] nvarchar(20) NOT NULL,
    [NextValue] bigint NOT NULL,
    CONSTRAINT [PK_NumberSequences] PRIMARY KEY ([Name])
);
GO


CREATE TABLE [Policies] (
    [Id] uniqueidentifier NOT NULL,
    [PolicyNumber] nvarchar(20) NOT NULL,
    [QuoteId] uniqueidentifier NOT NULL,
    [AgencyCode] nvarchar(10) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [InceptionDate] date NOT NULL,
    [ExpiryDate] date NOT NULL,
    [ProductVersion] nvarchar(10) NOT NULL,
    [RiskJson] nvarchar(max) NOT NULL,
    [PremiumJson] nvarchar(max) NOT NULL,
    [ClausesJson] nvarchar(1000) NOT NULL,
    [InsuredName] nvarchar(200) NOT NULL,
    [Postcode] nvarchar(10) NOT NULL,
    [TradeCode] nvarchar(10) NOT NULL,
    [AnnualGrossPremium] decimal(18,2) NOT NULL,
    [CommissionRate] decimal(5,4) NOT NULL,
    [PreviousPolicyId] uniqueidentifier NULL,
    [RenewalQuoteId] uniqueidentifier NULL,
    [RenewalInvitedUtc] datetime2 NULL,
    [CreatedUtc] datetime2 NOT NULL,
    [ConcurrencyStamp] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Policies] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Policies_Agencies_AgencyCode] FOREIGN KEY ([AgencyCode]) REFERENCES [Agencies] ([Code]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Quotes] (
    [Id] uniqueidentifier NOT NULL,
    [Reference] nvarchar(20) NOT NULL,
    [AgencyCode] nvarchar(10) NOT NULL,
    [BrokerReference] nvarchar(200) NULL,
    [CreatedBy] nvarchar(200) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [ProductVersion] nvarchar(10) NOT NULL,
    [InceptionDate] date NOT NULL,
    [ValidUntil] date NULL,
    [CommissionRate] decimal(5,4) NOT NULL,
    [RiskJson] nvarchar(max) NOT NULL,
    [PremiumJson] nvarchar(max) NULL,
    [LoadingsJson] nvarchar(1000) NOT NULL,
    [ClausesJson] nvarchar(1000) NOT NULL,
    [InsuredName] nvarchar(200) NOT NULL,
    [Postcode] nvarchar(10) NOT NULL,
    [TradeCode] nvarchar(10) NOT NULL,
    [TechnicalPremium] decimal(18,2) NULL,
    [GrossPremium] decimal(18,2) NULL,
    [Total] decimal(18,2) NULL,
    [RatingEngine] nvarchar(200) NULL,
    [RatingTables] nvarchar(200) NULL,
    [RatingError] nvarchar(1000) NULL,
    [RatingMilliseconds] int NOT NULL,
    [ReferredUtc] datetime2 NULL,
    [AssignedUnderwriter] nvarchar(200) NULL,
    [DecidedBy] nvarchar(200) NULL,
    [DecidedUtc] datetime2 NULL,
    [UnderwriterNote] nvarchar(2000) NULL,
    [PolicyId] uniqueidentifier NULL,
    [RenewalOfPolicyId] uniqueidentifier NULL,
    [CreatedUtc] datetime2 NOT NULL,
    [UpdatedUtc] datetime2 NOT NULL,
    [ConcurrencyStamp] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Quotes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Quotes_Agencies_AgencyCode] FOREIGN KEY ([AgencyCode]) REFERENCES [Agencies] ([Code]) ON DELETE NO ACTION
);
GO


CREATE TABLE [PolicyTransactions] (
    [Id] uniqueidentifier NOT NULL,
    [PolicyId] uniqueidentifier NOT NULL,
    [Type] nvarchar(20) NOT NULL,
    [EffectiveDate] date NOT NULL,
    [GrossPremium] decimal(18,2) NOT NULL,
    [Ipt] decimal(18,2) NOT NULL,
    [Commission] decimal(18,2) NOT NULL,
    [Fee] decimal(18,2) NOT NULL,
    [SectionGrossJson] nvarchar(1000) NOT NULL,
    [Reason] nvarchar(400) NULL,
    [CreatedBy] nvarchar(200) NOT NULL,
    [CreatedUtc] datetime2 NOT NULL,
    [BordereauRunId] uniqueidentifier NULL,
    CONSTRAINT [PK_PolicyTransactions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PolicyTransactions_Policies_PolicyId] FOREIGN KEY ([PolicyId]) REFERENCES [Policies] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [QuoteEvents] (
    [Id] bigint NOT NULL IDENTITY,
    [QuoteId] uniqueidentifier NOT NULL,
    [AtUtc] datetime2 NOT NULL,
    [Event] nvarchar(40) NOT NULL,
    [By] nvarchar(200) NULL,
    [Detail] nvarchar(2000) NULL,
    CONSTRAINT [PK_QuoteEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_QuoteEvents_Quotes_QuoteId] FOREIGN KEY ([QuoteId]) REFERENCES [Quotes] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [QuoteReferrals] (
    [Id] int NOT NULL IDENTITY,
    [QuoteId] uniqueidentifier NOT NULL,
    [Code] nvarchar(10) NOT NULL,
    [Text] nvarchar(400) NOT NULL,
    [Source] nvarchar(20) NOT NULL,
    CONSTRAINT [PK_QuoteReferrals] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_QuoteReferrals_Quotes_QuoteId] FOREIGN KEY ([QuoteId]) REFERENCES [Quotes] ([Id]) ON DELETE CASCADE
);
GO


CREATE INDEX [IX_BordereauRuns_Period] ON [BordereauRuns] ([Period]);
GO


CREATE INDEX [IX_Documents_PolicyId] ON [Documents] ([PolicyId]);
GO


CREATE INDEX [IX_Documents_QuoteId] ON [Documents] ([QuoteId]);
GO


CREATE INDEX [IX_Policies_AgencyCode] ON [Policies] ([AgencyCode]);
GO


CREATE UNIQUE INDEX [IX_Policies_PolicyNumber] ON [Policies] ([PolicyNumber]);
GO


CREATE INDEX [IX_Policies_Status_ExpiryDate] ON [Policies] ([Status], [ExpiryDate]);
GO


CREATE INDEX [IX_PolicyTransactions_CreatedUtc_BordereauRunId] ON [PolicyTransactions] ([CreatedUtc], [BordereauRunId]);
GO


CREATE INDEX [IX_PolicyTransactions_PolicyId] ON [PolicyTransactions] ([PolicyId]);
GO


CREATE INDEX [IX_QuoteEvents_QuoteId] ON [QuoteEvents] ([QuoteId]);
GO


CREATE INDEX [IX_QuoteReferrals_QuoteId] ON [QuoteReferrals] ([QuoteId]);
GO


CREATE INDEX [IX_Quotes_AgencyCode_Status] ON [Quotes] ([AgencyCode], [Status]);
GO


CREATE UNIQUE INDEX [IX_Quotes_Reference] ON [Quotes] ([Reference]);
GO


CREATE INDEX [IX_Quotes_Status_ReferredUtc] ON [Quotes] ([Status], [ReferredUtc]);
GO


