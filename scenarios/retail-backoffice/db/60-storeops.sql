CREATE TABLE [CashUp] (
    [Id] bigint NOT NULL IDENTITY,
    [StoreCode] nvarchar(10) NULL,
    [BusinessDate] datetime2 NOT NULL,
    [ExpectedCash] decimal(18,2) NOT NULL,
    [CountedCash] decimal(18,2) NOT NULL,
    [Difference] decimal(18,2) NOT NULL,
    [CountedBy] nvarchar(100) NULL,
    [Notes] nvarchar(500) NULL,
    [CreatedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_CashUp] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Collection] (
    [Id] bigint NOT NULL IDENTITY,
    [StoreCode] nvarchar(10) NULL,
    [OrderId] uniqueidentifier NOT NULL,
    [OrderNo] nvarchar(30) NULL,
    [CustomerEmail] nvarchar(200) NULL,
    [CustomerPhone] nvarchar(30) NULL,
    [Status] nvarchar(20) NULL,
    [ReadyUtc] datetime2 NOT NULL,
    [CollectBy] datetime2 NOT NULL,
    [CollectedUtc] datetime2 NULL,
    [HandedOverBy] nvarchar(100) NULL,
    CONSTRAINT [PK_Collection] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [DailyTakings] (
    [Id] bigint NOT NULL IDENTITY,
    [StoreCode] nvarchar(10) NULL,
    [BusinessDate] datetime2 NOT NULL,
    [Receipts] int NOT NULL,
    [Items] int NOT NULL,
    [NetSales] decimal(18,2) NOT NULL,
    [ReturnsValue] decimal(18,2) NOT NULL,
    [CashTotal] decimal(18,2) NOT NULL,
    [CardTotal] decimal(18,2) NOT NULL,
    [OtherTotal] decimal(18,2) NOT NULL,
    [LoyaltyReceipts] int NOT NULL,
    [LastFile] nvarchar(100) NULL,
    [UpdatedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_DailyTakings] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Footfall] (
    [Id] bigint NOT NULL IDENTITY,
    [StoreCode] nvarchar(10) NULL,
    [Date] datetime2 NOT NULL,
    [Hour] int NOT NULL,
    [Visitors] int NOT NULL,
    CONSTRAINT [PK_Footfall] PRIMARY KEY ([Id])
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


CREATE TABLE [PickTask] (
    [Id] uniqueidentifier NOT NULL,
    [StoreCode] nvarchar(10) NULL,
    [OrderId] uniqueidentifier NOT NULL,
    [OrderNo] nvarchar(30) NULL,
    [ShipmentId] uniqueidentifier NOT NULL,
    [DeliveryMethod] nvarchar(20) NULL,
    [CustomerName] nvarchar(200) NULL,
    [Status] nvarchar(20) NULL,
    [AssignedTo] nvarchar(100) NULL,
    [Parcels] int NOT NULL,
    [WeightKg] decimal(9,3) NOT NULL,
    [CreatedUtc] datetime2 NOT NULL,
    [DueUtc] datetime2 NOT NULL,
    [PackedUtc] datetime2 NULL,
    CONSTRAINT [PK_PickTask] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Store] (
    [Code] nvarchar(10) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Type] nvarchar(10) NOT NULL,
    [Region] nvarchar(30) NULL,
    [Country] nvarchar(2) NOT NULL,
    [City] nvarchar(60) NULL,
    [Street] nvarchar(100) NULL,
    [PostalCode] nvarchar(10) NULL,
    [Currency] nvarchar(3) NULL,
    [TimeZone] nvarchar(40) NULL,
    [Phone] nvarchar(30) NULL,
    [ManagerEmail] nvarchar(200) NULL,
    [ShipFromStore] bit NOT NULL,
    [ClickAndCollect] bit NOT NULL,
    [OpeningHours] nvarchar(500) NULL,
    [OpenedOn] datetime2 NOT NULL,
    [ClosedOn] datetime2 NULL,
    [UpdatedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Store] PRIMARY KEY ([Code])
);
GO


CREATE TABLE [PickTaskLine] (
    [Id] bigint NOT NULL IDENTITY,
    [PickTaskId] uniqueidentifier NOT NULL,
    [Sku] nvarchar(40) NULL,
    [Quantity] int NOT NULL,
    [Picked] int NOT NULL,
    CONSTRAINT [PK_PickTaskLine] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PickTaskLine_PickTask_PickTaskId] FOREIGN KEY ([PickTaskId]) REFERENCES [PickTask] ([Id]) ON DELETE CASCADE
);
GO


CREATE INDEX [IX_CashUp_StoreCode_BusinessDate] ON [CashUp] ([StoreCode], [BusinessDate]);
GO


CREATE INDEX [IX_Collection_OrderNo] ON [Collection] ([OrderNo]);
GO


CREATE INDEX [IX_Collection_StoreCode_Status] ON [Collection] ([StoreCode], [Status]);
GO


CREATE UNIQUE INDEX [IX_DailyTakings_StoreCode_BusinessDate] ON [DailyTakings] ([StoreCode], [BusinessDate]) WHERE [StoreCode] IS NOT NULL;
GO


CREATE UNIQUE INDEX [IX_Footfall_StoreCode_Date_Hour] ON [Footfall] ([StoreCode], [Date], [Hour]) WHERE [StoreCode] IS NOT NULL;
GO


CREATE UNIQUE INDEX [IX_OutboxMessage_MessageId] ON [OutboxMessage] ([MessageId]);
GO


CREATE INDEX [IX_OutboxMessage_SentUtc_Id] ON [OutboxMessage] ([SentUtc], [Id]);
GO


CREATE UNIQUE INDEX [IX_PickTask_ShipmentId] ON [PickTask] ([ShipmentId]);
GO


CREATE INDEX [IX_PickTask_StoreCode_Status] ON [PickTask] ([StoreCode], [Status]);
GO


CREATE INDEX [IX_PickTaskLine_PickTaskId] ON [PickTaskLine] ([PickTaskId]);
GO


