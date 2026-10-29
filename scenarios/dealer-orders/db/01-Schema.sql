-- Dealer Orders schema. dlr = portal and sales data, ax = command queue to Dynamics AX, edi = EDI partners and messages,
-- cache = prices, stock, items and rates read from AX by the sync service. ASP.NET Identity 2 tables are in dbo.
-- The table definitions match the EF6 model in Tailspin.DealerOrders.Data (the database is not created by EF).
USE [DealerOrders];
GO
-- Filtered indexes below need these settings in every session that writes to the tables (SqlClient default).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF SCHEMA_ID(N'dlr') IS NULL EXEC(N'CREATE SCHEMA [dlr]');
IF SCHEMA_ID(N'ax') IS NULL EXEC(N'CREATE SCHEMA [ax]');
IF SCHEMA_ID(N'edi') IS NULL EXEC(N'CREATE SCHEMA [edi]');
IF SCHEMA_ID(N'cache') IS NULL EXEC(N'CREATE SCHEMA [cache]');
GO

CREATE TABLE [dlr].[Approval] (
    [Id] int NOT NULL IDENTITY,
    [QuoteId] int NULL,
    [OrderId] int NULL,
    [DealerId] int NOT NULL,
    [Reason] int NOT NULL,
    [Details] nvarchar(1000) NULL,
    [Amount] decimal(19, 4) NOT NULL,
    [Currency] char(3) NULL,
    [RequestedDiscount] decimal(19, 4) NOT NULL,
    [RequestedUtc] datetime2(7) NOT NULL,
    [RequestedBy] nvarchar(256) NULL,
    [Decision] int NOT NULL,
    [DecidedBy] nvarchar(256) NULL,
    [DecidedUtc] datetime2(7) NULL,
    [DecisionComment] nvarchar(1000) NULL,
    [ApprovedDiscount] decimal(19, 4) NULL,
    PRIMARY KEY (Id)
);
GO

CREATE TABLE [ax].[Command] (
    [Id] bigint NOT NULL IDENTITY,
    [CommandType] int NOT NULL,
    [State] int NOT NULL,
    [DealerId] int NULL,
    [QuoteId] int NULL,
    [OrderId] int NULL,
    [SequenceKey] nvarchar(40) NOT NULL,
    [Payload] nvarchar(max) NULL,
    [RequestedBy] nvarchar(256) NULL,
    [RunAsAxUser] nvarchar(20) NULL,
    [CreatedUtc] datetime2(7) NOT NULL,
    [NextAttemptUtc] datetime2(7) NOT NULL,
    [Attempts] int NOT NULL,
    [CompletedUtc] datetime2(7) NULL,
    [LastError] nvarchar(4000) NULL,
    [Result] nvarchar(1000) NULL,
    [ClaimedUtc] datetime2(7) NULL,
    [ClaimedBy] nvarchar(128) NULL,
    PRIMARY KEY (Id)
);
GO

CREATE TABLE [dlr].[Dealer] (
    [Id] int NOT NULL IDENTITY,
    [AccountNumber] nvarchar(20) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [CountryCode] char(2) NULL,
    [Currency] char(3) NULL,
    [LanguageCode] varchar(5) NULL,
    [PriceGroup] nvarchar(10) NULL,
    [DiscountTier] int NOT NULL,
    [MaxSelfApprovedDiscount] decimal(19, 4) NOT NULL,
    [CreditLimit] decimal(19, 4) NOT NULL,
    [OpenBalance] decimal(19, 4) NOT NULL,
    [CreditHold] bit NOT NULL,
    [PaymentTerms] nvarchar(10) NULL,
    [DeliveryTerms] int NOT NULL,
    [Warehouse] nvarchar(10) NULL,
    [SalesDistrict] nvarchar(10) NULL,
    [SalesResponsible] nvarchar(60) NULL,
    [MainDealerAccount] nvarchar(20) NULL,
    [Email] nvarchar(254) NULL,
    [Phone] nvarchar(30) NULL,
    [Status] int NOT NULL,
    [EdiPartnerId] int NULL,
    [LastSyncedUtc] datetime2(7) NULL,
    [RowVersion] rowversion NOT NULL,
    PRIMARY KEY (Id)
);
GO

CREATE TABLE [dlr].[DealerAddress] (
    [Id] int NOT NULL IDENTITY,
    [DealerId] int NOT NULL,
    [AxLocationId] nvarchar(20) NULL,
    [Name] nvarchar(100) NOT NULL,
    [Street] nvarchar(250) NULL,
    [PostalCode] nvarchar(10) NULL,
    [City] nvarchar(60) NULL,
    [State] nvarchar(10) NULL,
    [CountryCode] char(2) NULL,
    [IsDefaultDelivery] bit NOT NULL,
    [EdiLocationCode] nvarchar(20) NULL,
    PRIMARY KEY (Id)
);
GO

CREATE TABLE [edi].[Message] (
    [Id] int NOT NULL IDENTITY,
    [PartnerId] int NOT NULL,
    [Direction] int NOT NULL,
    [TransactionSet] nvarchar(20) NULL,
    [InterchangeControl] nvarchar(9) NULL,
    [GroupControl] nvarchar(60) NULL,
    [TransactionControl] nvarchar(9) NULL,
    [Reference] nvarchar(100) NULL,
    [State] int NOT NULL,
    [Content] nvarchar(max) NULL,
    [As2MessageId] nvarchar(200) NULL,
    [MdnStatus] nvarchar(200) NULL,
    [CreatedUtc] datetime2(7) NOT NULL,
    [SentUtc] datetime2(7) NULL,
    [Attempts] int NOT NULL,
    [Error] nvarchar(2000) NULL,
    [OrderId] int NULL,
    PRIMARY KEY (Id)
);
GO

CREATE TABLE [edi].[Partner] (
    [Id] int NOT NULL,
    [Name] nvarchar(60) NOT NULL,
    [DealerId] int NOT NULL,
    [Transport] int NOT NULL,
    [InterchangeQualifier] nvarchar(2) NULL,
    [InterchangeId] nvarchar(15) NULL,
    [ApplicationCode] nvarchar(15) NULL,
    [As2Id] nvarchar(60) NULL,
    [As2Url] nvarchar(250) NULL,
    [PartnerCertificateFile] nvarchar(260) NULL,
    [EncryptOutbound] bit NOT NULL,
    [RequestSignedMdn] bit NOT NULL,
    [SftpHost] nvarchar(100) NULL,
    [SftpPort] int NOT NULL,
    [SftpUser] nvarchar(60) NULL,
    [SftpInboundFolder] nvarchar(100) NULL,
    [SftpOutboundFolder] nvarchar(100) NULL,
    [Send855] bit NOT NULL,
    [Send856] bit NOT NULL,
    [Active] bit NOT NULL,
    [LastInterchangeControl] int NOT NULL,
    PRIMARY KEY (Id)
);
GO

CREATE TABLE [dbo].[AspNetRoles] (
    [Id] nvarchar(128) NOT NULL,
    [Name] nvarchar(256) NOT NULL,
    PRIMARY KEY (Id)
);
GO

CREATE TABLE [dbo].[AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(128) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    PRIMARY KEY (Id)
);
GO

CREATE TABLE [dbo].[AspNetUserLogins] (
    [LoginProvider] nvarchar(128) NOT NULL,
    [ProviderKey] nvarchar(128) NOT NULL,
    [UserId] nvarchar(128) NOT NULL,
    PRIMARY KEY ([LoginProvider], [ProviderKey], [UserId])
);
GO

CREATE TABLE [dbo].[AspNetUserRoles] (
    [UserId] nvarchar(128) NOT NULL,
    [RoleId] nvarchar(128) NOT NULL,
    PRIMARY KEY ([UserId], [RoleId])
);
GO

CREATE TABLE [dlr].[Notification] (
    [Id] bigint NOT NULL IDENTITY,
    [Kind] int NOT NULL,
    [DealerId] int NULL,
    [OrderId] int NULL,
    [QuoteId] int NULL,
    [Recipient] nvarchar(254) NULL,
    [Subject] nvarchar(200) NULL,
    [Body] nvarchar(max) NULL,
    [PushPayload] nvarchar(max) NULL,
    [CreatedUtc] datetime2(7) NOT NULL,
    [MailSentUtc] datetime2(7) NULL,
    [PushedUtc] datetime2(7) NULL,
    [Attempts] int NOT NULL,
    [LastError] nvarchar(1000) NULL,
    PRIMARY KEY (Id)
);
GO

CREATE TABLE [dlr].[OrderLine] (
    [Id] int NOT NULL IDENTITY,
    [OrderId] int NOT NULL,
    [LineNumber] int NOT NULL,
    [ItemId] nvarchar(20) NOT NULL,
    [ModelCode] nvarchar(20) NULL,
    [ConfigurationId] nvarchar(10) NULL,
    [OptionCodes] nvarchar(1000) NULL,
    [Description] nvarchar(200) NULL,
    [Quantity] decimal(19, 4) NOT NULL,
    [ListPrice] decimal(19, 4) NOT NULL,
    [DiscountPercent] decimal(19, 4) NOT NULL,
    [NetPrice] decimal(19, 4) NOT NULL,
    [DealerLineReference] nvarchar(20) NULL,
    [DealerItemNumber] nvarchar(40) NULL,
    [AxInventTransId] nvarchar(20) NULL,
    [Reservation] int NOT NULL,
    [ReservedQuantity] decimal(19, 4) NOT NULL,
    [ShippedQuantity] decimal(19, 4) NOT NULL,
    [InvoicedQuantity] decimal(19, 4) NOT NULL,
    [ConfirmedShipDate] date NULL,
    [SerialNumber] nvarchar(30) NULL,
    [Cancelled] bit NOT NULL,
    PRIMARY KEY (Id)
);
GO

CREATE TABLE [dlr].[OrderStatusChange] (
    [Id] int NOT NULL IDENTITY,
    [OrderId] int NOT NULL,
    [FromStatus] int NOT NULL,
    [ToStatus] int NOT NULL,
    [ChangedBy] nvarchar(256) NULL,
    [ChangedUtc] datetime2(7) NOT NULL,
    [Comment] nvarchar(1000) NULL,
    PRIMARY KEY (Id)
);
GO

CREATE TABLE [dbo].[AspNetUsers] (
    [Id] nvarchar(128) NOT NULL,
    [DealerId] int NOT NULL,
    [DisplayName] nvarchar(max) NULL,
    [LanguageCode] nvarchar(max) NULL,
    [CreatedUtc] datetime2(7) NOT NULL,
    [LastLoginUtc] datetime2(7) NULL,
    [Disabled] bit NOT NULL,
    [Email] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEndDateUtc] datetime2(7) NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    [UserName] nvarchar(256) NOT NULL,
    PRIMARY KEY (Id)
);
GO

CREATE TABLE [dlr].[Quote] (
    [Id] int NOT NULL IDENTITY,
    [QuoteNumber] nvarchar(20) NOT NULL,
    [DealerId] int NOT NULL,
    [CreatedBy] nvarchar(256) NULL,
    [CreatedUtc] datetime2(7) NOT NULL,
    [ValidUntil] date NOT NULL,
    [Status] int NOT NULL,
    [Currency] char(3) NULL,
    [EndCustomerName] nvarchar(100) NULL,
    [EndCustomerCountry] char(2) NULL,
    [Reference] nvarchar(60) NULL,
    [RequestedExtraDiscount] decimal(19, 4) NOT NULL,
    [ApprovedExtraDiscount] decimal(19, 4) NOT NULL,
    [ApprovalComment] nvarchar(1000) NULL,
    [AxQuotationId] nvarchar(20) NULL,
    [OrderId] int NULL,
    [Notes] nvarchar(max) NULL,
    [RowVersion] rowversion NOT NULL,
    PRIMARY KEY (Id)
);
GO

CREATE TABLE [dlr].[QuoteLine] (
    [Id] int NOT NULL IDENTITY,
    [QuoteId] int NOT NULL,
    [LineNumber] int NOT NULL,
    [ItemId] nvarchar(20) NOT NULL,
    [ModelCode] nvarchar(20) NULL,
    [ConfigurationId] nvarchar(10) NULL,
    [OptionCodes] nvarchar(1000) NULL,
    [Description] nvarchar(200) NULL,
    [Quantity] decimal(19, 4) NOT NULL,
    [ListPrice] decimal(19, 4) NOT NULL,
    [DiscountPercent] decimal(19, 4) NOT NULL,
    [NetPrice] decimal(19, 4) NOT NULL,
    [LeadTimeWeeks] int NOT NULL,
    PRIMARY KEY (Id)
);
GO

CREATE TABLE [dlr].[SalesOrder] (
    [Id] int NOT NULL IDENTITY,
    [OrderNumber] nvarchar(20) NOT NULL,
    [DealerId] int NOT NULL,
    [Source] int NOT NULL,
    [OrderType] int NOT NULL,
    [Status] int NOT NULL,
    [DealerPoNumber] nvarchar(40) NULL,
    [Currency] char(3) NULL,
    [CreatedUtc] datetime2(7) NOT NULL,
    [CreatedBy] nvarchar(256) NULL,
    [RequestedDeliveryDate] date NULL,
    [ConfirmedDeliveryDate] date NULL,
    [DeliveryAddressId] int NULL,
    [DeliveryTerms] int NOT NULL,
    [EndCustomerName] nvarchar(100) NULL,
    [QuoteId] int NULL,
    [ExtraDiscount] decimal(19, 4) NOT NULL,
    [FreightCharge] decimal(19, 4) NOT NULL,
    [AxSalesId] nvarchar(20) NULL,
    [AxStatusText] nvarchar(100) NULL,
    [AxLastReadUtc] datetime2(7) NULL,
    [ErrorMessage] nvarchar(2000) NULL,
    [EdiMessageId] int NULL,
    [Notes] nvarchar(4000) NULL,
    [RowVersion] rowversion NOT NULL,
    PRIMARY KEY (Id)
);
GO

CREATE TABLE [dlr].[Shipment] (
    [Id] int NOT NULL IDENTITY,
    [OrderId] int NOT NULL,
    [PackingSlipId] nvarchar(20) NOT NULL,
    [ShipDate] date NOT NULL,
    [CarrierCode] nvarchar(10) NULL,
    [TrackingNumber] nvarchar(40) NULL,
    [TrailerNumber] nvarchar(20) NULL,
    [GrossWeightKg] decimal(19, 4) NOT NULL,
    [Packages] int NOT NULL,
    [AsnSent] bit NOT NULL,
    PRIMARY KEY (Id)
);
GO

CREATE TABLE [dlr].[ShipmentLine] (
    [Id] int NOT NULL IDENTITY,
    [ShipmentId] int NOT NULL,
    [OrderLineNumber] int NOT NULL,
    [ItemId] nvarchar(20) NULL,
    [Quantity] decimal(19, 4) NOT NULL,
    [SerialNumber] nvarchar(30) NULL,
    PRIMARY KEY (Id)
);
GO

CREATE TABLE [dlr].[Document] (
    [Id] int NOT NULL IDENTITY,
    [Kind] int NOT NULL,
    [DealerId] int NULL,
    [QuoteId] int NULL,
    [OrderId] int NULL,
    [FileName] nvarchar(200) NOT NULL,
    [ContentType] nvarchar(100) NULL,
    [Content] varbinary(max) NULL,
    [Sha256] char(64) NULL,
    [CreatedUtc] datetime2(7) NOT NULL,
    [CreatedBy] nvarchar(256) NULL,
    PRIMARY KEY (Id)
);
GO

ALTER TABLE [dlr].[DealerAddress] ADD CONSTRAINT [Dealer_Addresses] FOREIGN KEY ([DealerId]) REFERENCES [dlr].[Dealer]([Id]) ON DELETE CASCADE;
GO
ALTER TABLE [dbo].[AspNetUserRoles] ADD CONSTRAINT [IdentityRole_Users] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles]([Id]) ON DELETE CASCADE;
GO
ALTER TABLE [dbo].[AspNetUserClaims] ADD CONSTRAINT [PortalUser_Claims] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE;
GO
ALTER TABLE [dbo].[AspNetUserLogins] ADD CONSTRAINT [PortalUser_Logins] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE;
GO
ALTER TABLE [dbo].[AspNetUserRoles] ADD CONSTRAINT [PortalUser_Roles] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE;
GO
ALTER TABLE [dlr].[Quote] ADD CONSTRAINT [Quote_Dealer] FOREIGN KEY ([DealerId]) REFERENCES [dlr].[Dealer]([Id]);
GO
ALTER TABLE [dlr].[QuoteLine] ADD CONSTRAINT [Quote_Lines] FOREIGN KEY ([QuoteId]) REFERENCES [dlr].[Quote]([Id]) ON DELETE CASCADE;
GO
ALTER TABLE [dlr].[SalesOrder] ADD CONSTRAINT [SalesOrder_Dealer] FOREIGN KEY ([DealerId]) REFERENCES [dlr].[Dealer]([Id]);
GO
ALTER TABLE [dlr].[SalesOrder] ADD CONSTRAINT [SalesOrder_DeliveryAddress] FOREIGN KEY ([DeliveryAddressId]) REFERENCES [dlr].[DealerAddress]([Id]);
GO

ALTER TABLE [dlr].[OrderStatusChange] ADD CONSTRAINT [SalesOrder_History] FOREIGN KEY ([OrderId]) REFERENCES [dlr].[SalesOrder]([Id]) ON DELETE CASCADE;
GO
ALTER TABLE [dlr].[OrderLine] ADD CONSTRAINT [SalesOrder_Lines] FOREIGN KEY ([OrderId]) REFERENCES [dlr].[SalesOrder]([Id]) ON DELETE CASCADE;
GO
ALTER TABLE [dlr].[ShipmentLine] ADD CONSTRAINT [Shipment_Lines] FOREIGN KEY ([ShipmentId]) REFERENCES [dlr].[Shipment]([Id]) ON DELETE CASCADE;
GO

-- Tables used only through Dapper -------------------------------------------------------------------------------

CREATE TABLE [dlr].[NumberSequence] (
    [Sequence] varchar(20) NOT NULL,
    [Year] int NOT NULL,
    [LastValue] bigint NOT NULL,
    CONSTRAINT [PK_NumberSequence] PRIMARY KEY ([Sequence], [Year])
);
GO

CREATE TABLE [dlr].[DiscountAgreement] (
    [Tier] int NOT NULL,
    [DiscountGroup] varchar(20) NOT NULL,
    [Percent] decimal(9, 4) NOT NULL,
    CONSTRAINT [PK_DiscountAgreement] PRIMARY KEY ([Tier], [DiscountGroup])
);
GO

CREATE TABLE [dlr].[Campaign] (
    [Code] varchar(20) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [ModelCode] varchar(20) NULL,
    [OptionCode] varchar(30) NULL,
    [CountryCode] char(2) NULL,
    [Percent] decimal(9, 4) NOT NULL,
    [FromDate] date NOT NULL,
    [ToDate] date NOT NULL,
    CONSTRAINT [PK_Campaign] PRIMARY KEY ([Code])
);
GO

CREATE TABLE [dlr].[FreightRate] (
    [CountryCode] char(2) NOT NULL,
    [Zone] varchar(5) NOT NULL,
    [Family] int NOT NULL,
    [AmountEur] decimal(19, 4) NOT NULL,
    CONSTRAINT [PK_FreightRate] PRIMARY KEY ([CountryCode], [Zone], [Family])
);
GO

-- cache.Price and cache.Stock are replaced in full by the sync service (SELECT INTO #staging + SqlBulkCopy); keep the
-- column order in step with SqlCacheStore.
CREATE TABLE [cache].[Price] (
    [ItemId] nvarchar(20) NOT NULL,
    [PriceGroup] nvarchar(10) NOT NULL,
    [Currency] char(3) NOT NULL,
    [Amount] decimal(19, 4) NOT NULL,
    [QuantityFrom] decimal(19, 4) NOT NULL,
    [FromDate] date NOT NULL,
    [ToDate] date NULL
);
GO
CREATE CLUSTERED INDEX [IX_Price_Item] ON [cache].[Price] ([ItemId], [PriceGroup], [Currency]);
GO

CREATE TABLE [cache].[Stock] (
    [ItemId] nvarchar(20) NOT NULL,
    [ConfigurationId] nvarchar(10) NOT NULL,
    [Warehouse] nvarchar(10) NOT NULL,
    [PhysicalOnHand] decimal(19, 4) NOT NULL,
    [Reserved] decimal(19, 4) NOT NULL,
    [OnOrder] decimal(19, 4) NOT NULL,
    [NextReceiptDate] date NULL,
    [UpdatedUtc] datetime2(7) NOT NULL
);
GO
CREATE CLUSTERED INDEX [IX_Stock_Item] ON [cache].[Stock] ([ItemId], [ConfigurationId], [Warehouse]);
GO

CREATE TABLE [cache].[ExchangeRate] (
    [FromCurrency] char(3) NOT NULL,
    [ToCurrency] char(3) NOT NULL,
    [ValidFrom] date NOT NULL,
    [Rate] decimal(19, 8) NOT NULL,
    CONSTRAINT [PK_ExchangeRate] PRIMARY KEY ([FromCurrency], [ToCurrency], [ValidFrom])
);
GO

CREATE TABLE [cache].[Item] (
    [ItemId] nvarchar(20) NOT NULL,
    [Name] nvarchar(100) NULL,
    [ItemGroup] nvarchar(20) NULL,
    [NetWeightKg] decimal(19, 4) NOT NULL,
    [Stopped] bit NOT NULL,
    CONSTRAINT [PK_Item] PRIMARY KEY ([ItemId])
);
GO

CREATE TABLE [cache].[SyncRun] (
    [Job] varchar(40) NOT NULL,
    [LastRunUtc] datetime2(7) NOT NULL,
    [Records] int NOT NULL,
    CONSTRAINT [PK_SyncRun] PRIMARY KEY ([Job])
);
GO

-- Indexes ------------------------------------------------------------------------------------------------------

CREATE UNIQUE INDEX [UX_Dealer_Account] ON [dlr].[Dealer] ([AccountNumber]);
CREATE UNIQUE INDEX [UX_Quote_Number] ON [dlr].[Quote] ([QuoteNumber]);
CREATE INDEX [IX_Quote_Dealer] ON [dlr].[Quote] ([DealerId], [Status]);
CREATE UNIQUE INDEX [UX_SalesOrder_Number] ON [dlr].[SalesOrder] ([OrderNumber]);
CREATE INDEX [IX_SalesOrder_Dealer] ON [dlr].[SalesOrder] ([DealerId], [Status]) INCLUDE ([CreatedUtc], [Currency]);
CREATE INDEX [IX_SalesOrder_AxSalesId] ON [dlr].[SalesOrder] ([AxSalesId]) WHERE [AxSalesId] IS NOT NULL;
CREATE INDEX [IX_SalesOrder_DealerPo] ON [dlr].[SalesOrder] ([DealerId], [DealerPoNumber]);
CREATE INDEX [IX_OrderLine_Order] ON [dlr].[OrderLine] ([OrderId]);
CREATE INDEX [IX_QuoteLine_Quote] ON [dlr].[QuoteLine] ([QuoteId]);
CREATE INDEX [IX_OrderStatusChange_Order] ON [dlr].[OrderStatusChange] ([OrderId]);
CREATE INDEX [IX_Approval_Pending] ON [dlr].[Approval] ([Decision]) INCLUDE ([DealerId], [QuoteId], [OrderId]);
CREATE INDEX [IX_Notification_Pending] ON [dlr].[Notification] ([MailSentUtc], [PushedUtc]) INCLUDE ([Attempts], [CreatedUtc]);
CREATE INDEX [IX_Document_Dealer] ON [dlr].[Document] ([DealerId], [CreatedUtc]) INCLUDE ([Kind], [QuoteId], [OrderId], [FileName]);
CREATE INDEX [IX_Shipment_Order] ON [dlr].[Shipment] ([OrderId]);
CREATE INDEX [IX_Command_Due] ON [ax].[Command] ([State], [NextAttemptUtc]) INCLUDE ([SequenceKey]);
CREATE INDEX [IX_Command_Sequence] ON [ax].[Command] ([SequenceKey], [Id]) INCLUDE ([State]);
CREATE INDEX [IX_Command_Order] ON [ax].[Command] ([OrderId]) WHERE [OrderId] IS NOT NULL;
CREATE INDEX [IX_Message_Partner] ON [edi].[Message] ([PartnerId], [Direction], [State]);
CREATE INDEX [IX_Message_Order] ON [edi].[Message] ([OrderId]) WHERE [OrderId] IS NOT NULL;
CREATE UNIQUE INDEX [UX_Partner_Interchange] ON [edi].[Partner] ([InterchangeQualifier], [InterchangeId]);
CREATE UNIQUE INDEX [UserNameIndex] ON [dbo].[AspNetUsers] ([UserName]);
CREATE INDEX [IX_AspNetUsers_Dealer] ON [dbo].[AspNetUsers] ([DealerId]);
CREATE UNIQUE INDEX [RoleNameIndex] ON [dbo].[AspNetRoles] ([Name]);
GO
