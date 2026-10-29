CREATE TABLE [AllocationDecision] (
    [Id] bigint NOT NULL IDENTITY,
    [OrderId] uniqueidentifier NOT NULL,
    [OrderNo] nvarchar(30) NULL,
    [Attempt] int NOT NULL,
    [Success] bit NOT NULL,
    [Strategy] nvarchar(30) NULL,
    [Reason] nvarchar(500) NULL,
    [DeliveryMethod] nvarchar(20) NULL,
    [Country] nvarchar(2) NULL,
    [ShipmentsJson] nvarchar(max) NULL,
    [ElapsedMs] bigint NOT NULL,
    [CreatedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_AllocationDecision] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AllocationRuleSet] (
    [Country] nvarchar(2) NOT NULL,
    [MaxShipments] int NOT NULL,
    [AllowShipFromStore] bit NOT NULL,
    [AllowCrossBorderStores] bit NOT NULL,
    [StoreMinimumLeft] int NOT NULL,
    [ReservationHours] int NOT NULL,
    [UpdatedUtc] datetime2 NOT NULL,
    [UpdatedBy] nvarchar(100) NULL,
    CONSTRAINT [PK_AllocationRuleSet] PRIMARY KEY ([Country])
);
GO


CREATE INDEX [IX_AllocationDecision_OrderId] ON [AllocationDecision] ([OrderId]);
GO


