IF OBJECT_ID(N'[exp].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'exp') IS NULL EXEC(N'CREATE SCHEMA [exp];');
    CREATE TABLE [exp].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    IF SCHEMA_ID(N'exp') IS NULL EXEC(N'CREATE SCHEMA [exp];');
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE TABLE [exp].[ClaimSequences] (
        [Year] int NOT NULL,
        [LastNumber] int NOT NULL,
        CONSTRAINT [PK_ClaimSequences] PRIMARY KEY ([Year])
    );
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE TABLE [exp].[Employees] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeNumber] nvarchar(20) NOT NULL,
        [Upn] nvarchar(200) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [Email] nvarchar(200) NOT NULL,
        [CostCenter] nvarchar(20) NOT NULL,
        [ManagerEmployeeNumber] nvarchar(20) NULL,
        [Iban] nvarchar(34) NULL,
        [Bic] nvarchar(11) NULL,
        [Active] bit NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Employees] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE TABLE [exp].[ExchangeRates] (
        [Date] date NOT NULL,
        [Currency] nchar(3) NOT NULL,
        [UnitsPerEuro] decimal(18,6) NOT NULL,
        CONSTRAINT [PK_ExchangeRates] PRIMARY KEY ([Date], [Currency])
    );
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE TABLE [exp].[PaymentRuns] (
        [Id] int NOT NULL IDENTITY,
        [MessageId] nvarchar(35) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ExecutionDate] date NOT NULL,
        [PaymentCount] int NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        [FileName] nvarchar(200) NOT NULL,
        [Status] int NOT NULL,
        [BankReference] nvarchar(100) NULL,
        [Error] nvarchar(2000) NULL,
        [SentAt] datetime2 NULL,
        CONSTRAINT [PK_PaymentRuns] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE TABLE [exp].[Receipts] (
        [Id] int NOT NULL IDENTITY,
        [FileId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(200) NOT NULL,
        [ContentType] nvarchar(100) NOT NULL,
        [Length] bigint NOT NULL,
        [Sha256] nvarchar(64) NOT NULL,
        [StoragePath] nvarchar(400) NOT NULL,
        [UploadedById] int NOT NULL,
        [UploadedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Receipts] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE TABLE [exp].[CompanyCards] (
        [Id] int NOT NULL IDENTITY,
        [MaskedNumber] nvarchar(19) NOT NULL,
        [EmployeeId] int NOT NULL,
        [Active] bit NOT NULL,
        CONSTRAINT [PK_CompanyCards] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CompanyCards_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [exp].[Employees] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE TABLE [exp].[Claims] (
        [Id] int NOT NULL IDENTITY,
        [Number] nvarchar(20) NOT NULL,
        [EmployeeId] int NOT NULL,
        [Purpose] nvarchar(500) NOT NULL,
        [CostCenter] nvarchar(20) NOT NULL,
        [ProjectCode] nvarchar(20) NULL,
        [Status] int NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        [Reimbursable] decimal(18,2) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [SubmittedAt] datetime2 NULL,
        [DecidedAt] datetime2 NULL,
        [CheckedAt] datetime2 NULL,
        [CheckedBy] nvarchar(200) NULL,
        [PaymentRunId] int NULL,
        [PaidAt] datetime2 NULL,
        [JournalDocumentNo] nvarchar(20) NULL,
        [ExportedAt] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Claims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Claims_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [exp].[Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Claims_PaymentRuns_PaymentRunId] FOREIGN KEY ([PaymentRunId]) REFERENCES [exp].[PaymentRuns] ([Id])
    );
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE TABLE [exp].[CardTransactions] (
        [Id] int NOT NULL IDENTITY,
        [Reference] nvarchar(40) NOT NULL,
        [CompanyCardId] int NOT NULL,
        [TransactionDate] date NOT NULL,
        [PostingDate] date NOT NULL,
        [Merchant] nvarchar(200) NOT NULL,
        [MerchantCity] nvarchar(60) NULL,
        [MerchantCountry] nvarchar(3) NULL,
        [MerchantCategoryCode] nchar(4) NOT NULL,
        [OriginalAmount] decimal(18,2) NOT NULL,
        [OriginalCurrency] nchar(3) NOT NULL,
        [BilledAmountEur] decimal(18,2) NOT NULL,
        [SourceFile] nvarchar(260) NOT NULL,
        [ImportedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CardTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CardTransactions_CompanyCards_CompanyCardId] FOREIGN KEY ([CompanyCardId]) REFERENCES [exp].[CompanyCards] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE TABLE [exp].[Approvals] (
        [Id] int NOT NULL IDENTITY,
        [ClaimId] int NOT NULL,
        [Level] int NOT NULL,
        [ApproverId] int NOT NULL,
        [Decision] int NOT NULL,
        [Comment] nvarchar(1000) NULL,
        [DecidedAt] datetime2 NULL,
        [RemindedAt] datetime2 NULL,
        CONSTRAINT [PK_Approvals] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Approvals_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [exp].[Claims] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Approvals_Employees_ApproverId] FOREIGN KEY ([ApproverId]) REFERENCES [exp].[Employees] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE TABLE [exp].[ClaimEvents] (
        [Id] bigint NOT NULL IDENTITY,
        [ClaimId] int NOT NULL,
        [At] datetime2 NOT NULL,
        [Actor] nvarchar(200) NOT NULL,
        [Action] nvarchar(50) NOT NULL,
        [Details] nvarchar(2000) NULL,
        CONSTRAINT [PK_ClaimEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ClaimEvents_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [exp].[Claims] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE TABLE [exp].[ClaimLines] (
        [Id] int NOT NULL IDENTITY,
        [ClaimId] int NOT NULL,
        [LineNumber] int NOT NULL,
        [ExpenseDate] date NOT NULL,
        [Category] int NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] nchar(3) NOT NULL,
        [ExchangeRate] decimal(18,6) NOT NULL,
        [AmountEur] decimal(18,2) NOT NULL,
        [Kilometres] decimal(9,1) NULL,
        [Route] nvarchar(200) NULL,
        [PerDiem] int NOT NULL,
        [Country] nchar(2) NULL,
        [Attendees] int NOT NULL,
        [VatRate] decimal(5,2) NOT NULL,
        [VatAmountEur] decimal(18,2) NOT NULL,
        [PaidByCompany] bit NOT NULL,
        [CardTransactionId] int NULL,
        [ReceiptId] int NULL,
        CONSTRAINT [PK_ClaimLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ClaimLines_CardTransactions_CardTransactionId] FOREIGN KEY ([CardTransactionId]) REFERENCES [exp].[CardTransactions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ClaimLines_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [exp].[Claims] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ClaimLines_Receipts_ReceiptId] FOREIGN KEY ([ReceiptId]) REFERENCES [exp].[Receipts] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE INDEX [IX_Approvals_ApproverId_Decision] ON [exp].[Approvals] ([ApproverId], [Decision]);
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE INDEX [IX_Approvals_ClaimId] ON [exp].[Approvals] ([ClaimId]);
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE INDEX [IX_CardTransactions_CompanyCardId_TransactionDate] ON [exp].[CardTransactions] ([CompanyCardId], [TransactionDate]);
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE UNIQUE INDEX [IX_CardTransactions_Reference] ON [exp].[CardTransactions] ([Reference]);
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE INDEX [IX_ClaimEvents_ClaimId] ON [exp].[ClaimEvents] ([ClaimId]);
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ClaimLines_CardTransactionId] ON [exp].[ClaimLines] ([CardTransactionId]) WHERE [CardTransactionId] IS NOT NULL');
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE UNIQUE INDEX [IX_ClaimLines_ClaimId_LineNumber] ON [exp].[ClaimLines] ([ClaimId], [LineNumber]);
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE INDEX [IX_ClaimLines_ReceiptId] ON [exp].[ClaimLines] ([ReceiptId]);
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE INDEX [IX_Claims_EmployeeId] ON [exp].[Claims] ([EmployeeId]);
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE UNIQUE INDEX [IX_Claims_Number] ON [exp].[Claims] ([Number]);
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE INDEX [IX_Claims_PaymentRunId] ON [exp].[Claims] ([PaymentRunId]);
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE INDEX [IX_Claims_Status_ExportedAt] ON [exp].[Claims] ([Status], [ExportedAt]);
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE INDEX [IX_Claims_Status_SubmittedAt] ON [exp].[Claims] ([Status], [SubmittedAt]);
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE INDEX [IX_CompanyCards_EmployeeId] ON [exp].[CompanyCards] ([EmployeeId]);
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE UNIQUE INDEX [IX_CompanyCards_MaskedNumber] ON [exp].[CompanyCards] ([MaskedNumber]);
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE UNIQUE INDEX [IX_Employees_EmployeeNumber] ON [exp].[Employees] ([EmployeeNumber]);
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE UNIQUE INDEX [IX_Employees_Upn] ON [exp].[Employees] ([Upn]);
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentRuns_MessageId] ON [exp].[PaymentRuns] ([MessageId]);
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    CREATE UNIQUE INDEX [IX_Receipts_FileId] ON [exp].[Receipts] ([FileId]);
END;
GO

IF NOT EXISTS(SELECT * FROM [exp].[__EFMigrationsHistory] WHERE [MigrationId] = N'20220316091522_InitialCreate')
BEGIN
    INSERT INTO [exp].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20220316091522_InitialCreate', N'6.0.10');
END;
GO

COMMIT;
GO

