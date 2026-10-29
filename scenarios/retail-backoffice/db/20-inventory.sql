CREATE TABLE [Item] (
    [Sku] nvarchar(40) NOT NULL,
    [StyleCode] nvarchar(20) NOT NULL,
    [Ean] nvarchar(13) NULL,
    [CategoryCode] nvarchar(20) NULL,
    [Discontinued] bit NOT NULL,
    [CreatedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Item] PRIMARY KEY ([Sku])
);
GO


CREATE TABLE [Location] (
    [Code] nvarchar(10) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Type] nvarchar(10) NOT NULL,
    [Country] nvarchar(2) NOT NULL,
    [City] nvarchar(60) NULL,
    [ShipFromStore] bit NOT NULL,
    [ClickAndCollect] bit NOT NULL,
    [Active] bit NOT NULL,
    [SafetyStock] int NOT NULL,
    [UpdatedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Location] PRIMARY KEY ([Code])
);
GO


CREATE TABLE [OutboxMessage] (
    [Id] bigint NOT NULL IDENTITY,
    [MessageId] uniqueidentifier NOT NULL,
    [MessageType] nvarchar(300) NOT NULL,
    [Body] nvarchar(max) NOT NULL,
    [CorrelationId] nvarchar(64) NULL,
    [CreatedUtc] datetime2 NOT NULL,
    [SentUtc] datetime2 NULL,
    [Attempts] int NOT NULL,
    [LastError] nvarchar(2000) NULL,
    CONSTRAINT [PK_OutboxMessage] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [ProcessedMessage] (
    [Key] nvarchar(200) NOT NULL,
    [ProcessedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ProcessedMessage] PRIMARY KEY ([Key])
);
GO


CREATE TABLE [ReplenishmentProposal] (
    [Id] uniqueidentifier NOT NULL,
    [StoreCode] nvarchar(10) NOT NULL,
    [FromLocation] nvarchar(10) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [CreatedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ReplenishmentProposal] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Reservation] (
    [Id] uniqueidentifier NOT NULL,
    [OrderId] uniqueidentifier NOT NULL,
    [ShipmentId] uniqueidentifier NOT NULL,
    [LocationCode] nvarchar(10) NOT NULL,
    [Sku] nvarchar(40) NOT NULL,
    [Quantity] int NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [CreatedUtc] datetime2 NOT NULL,
    [ExpiresUtc] datetime2 NOT NULL,
    [ClosedUtc] datetime2 NULL,
    CONSTRAINT [PK_Reservation] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [StockLevel] (
    [Id] bigint NOT NULL IDENTITY,
    [LocationCode] nvarchar(10) NOT NULL,
    [Sku] nvarchar(40) NOT NULL,
    [OnHand] int NOT NULL,
    [Reserved] int NOT NULL,
    [InTransit] int NOT NULL,
    [MinLevel] int NOT NULL,
    [MaxLevel] int NOT NULL,
    [UpdatedUtc] datetime2 NOT NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_StockLevel] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [StockMovement] (
    [Id] bigint NOT NULL IDENTITY,
    [LocationCode] nvarchar(10) NOT NULL,
    [Sku] nvarchar(40) NOT NULL,
    [Quantity] int NOT NULL,
    [Type] nvarchar(20) NOT NULL,
    [Reference] nvarchar(100) NULL,
    [CreatedBy] nvarchar(100) NULL,
    [CreatedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_StockMovement] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [ReplenishmentLine] (
    [Id] bigint NOT NULL IDENTITY,
    [ProposalId] uniqueidentifier NOT NULL,
    [Sku] nvarchar(40) NOT NULL,
    [Quantity] int NOT NULL,
    [OnHand] int NOT NULL,
    [MaxLevel] int NOT NULL,
    CONSTRAINT [PK_ReplenishmentLine] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReplenishmentLine_ReplenishmentProposal_ProposalId] FOREIGN KEY ([ProposalId]) REFERENCES [ReplenishmentProposal] ([Id]) ON DELETE CASCADE
);
GO


CREATE INDEX [IX_Item_StyleCode] ON [Item] ([StyleCode]);
GO


CREATE UNIQUE INDEX [IX_OutboxMessage_MessageId] ON [OutboxMessage] ([MessageId]);
GO


CREATE INDEX [IX_OutboxMessage_SentUtc_Id] ON [OutboxMessage] ([SentUtc], [Id]);
GO


CREATE INDEX [IX_ReplenishmentLine_ProposalId] ON [ReplenishmentLine] ([ProposalId]);
GO


CREATE INDEX [IX_Reservation_OrderId_ShipmentId] ON [Reservation] ([OrderId], [ShipmentId]);
GO


CREATE INDEX [IX_Reservation_Status_ExpiresUtc] ON [Reservation] ([Status], [ExpiresUtc]);
GO


CREATE UNIQUE INDEX [IX_StockLevel_LocationCode_Sku] ON [StockLevel] ([LocationCode], [Sku]);
GO


CREATE INDEX [IX_StockLevel_Sku] ON [StockLevel] ([Sku]);
GO


CREATE INDEX [IX_StockMovement_LocationCode_Sku_CreatedUtc] ON [StockMovement] ([LocationCode], [Sku], [CreatedUtc]);
GO


