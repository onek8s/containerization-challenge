CREATE TABLE [InboundWebhook] (
    [Id] bigint NOT NULL IDENTITY,
    [Source] nvarchar(20) NULL,
    [ExternalId] nvarchar(100) NULL,
    [Type] nvarchar(50) NULL,
    [Body] nvarchar(max) NULL,
    [SignatureValid] bit NOT NULL,
    [ReceivedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_InboundWebhook] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Order] (
    [Id] uniqueidentifier NOT NULL,
    [OrderNo] nvarchar(30) NOT NULL,
    [Channel] nvarchar(10) NULL,
    [Market] nvarchar(2) NULL,
    [Currency] nvarchar(3) NULL,
    [Status] nvarchar(20) NULL,
    [CustomerEmail] nvarchar(200) NULL,
    [CustomerFirstName] nvarchar(100) NULL,
    [CustomerLastName] nvarchar(100) NULL,
    [CustomerPhone] nvarchar(30) NULL,
    [LoyaltyCardNo] nvarchar(20) NULL,
    [DeliveryMethod] nvarchar(20) NULL,
    [CollectStoreCode] nvarchar(10) NULL,
    [ShipToName] nvarchar(200) NULL,
    [ShipToStreet] nvarchar(200) NULL,
    [ShipToPostalCode] nvarchar(20) NULL,
    [ShipToCity] nvarchar(100) NULL,
    [ShipToCountry] nvarchar(2) NULL,
    [ShippingFee] decimal(18,2) NOT NULL,
    [Total] decimal(18,2) NOT NULL,
    [PromotionCode] nvarchar(30) NULL,
    [PaymentReference] nvarchar(100) NULL,
    [PaymentMethod] nvarchar(30) NULL,
    [PaymentStatus] nvarchar(20) NULL,
    [CapturedAmount] decimal(18,2) NOT NULL,
    [CreatedBy] nvarchar(100) NULL,
    [PlacedUtc] datetime2 NOT NULL,
    [UpdatedUtc] datetime2 NOT NULL,
    [CancelReason] nvarchar(500) NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_Order] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [OrderState] (
    [CorrelationId] uniqueidentifier NOT NULL,
    [CurrentState] nvarchar(40) NULL,
    [OrderNo] nvarchar(30) NULL,
    [DeliveryMethod] nvarchar(20) NULL,
    [PaymentAuthorized] bit NOT NULL,
    [PaymentCaptured] bit NOT NULL,
    [ShipmentsTotal] int NOT NULL,
    [ShipmentsDispatched] int NOT NULL,
    [ShipmentsDelivered] int NOT NULL,
    [CreatedUtc] datetime2 NOT NULL,
    [UpdatedUtc] datetime2 NOT NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_OrderState] PRIMARY KEY ([CorrelationId])
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


CREATE TABLE [OrderEvent] (
    [Id] bigint NOT NULL IDENTITY,
    [OrderId] uniqueidentifier NOT NULL,
    [Type] nvarchar(40) NULL,
    [Text] nvarchar(1000) NULL,
    [By] nvarchar(100) NULL,
    [CreatedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_OrderEvent] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OrderEvent_Order_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Order] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [OrderLine] (
    [Id] bigint NOT NULL IDENTITY,
    [OrderId] uniqueidentifier NOT NULL,
    [LineNo] int NOT NULL,
    [Sku] nvarchar(40) NULL,
    [Description] nvarchar(200) NULL,
    [Quantity] int NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [Discount] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_OrderLine] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OrderLine_Order_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Order] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Shipment] (
    [Id] uniqueidentifier NOT NULL,
    [OrderId] uniqueidentifier NOT NULL,
    [FromLocation] nvarchar(10) NULL,
    [FromLocationType] nvarchar(10) NULL,
    [Status] nvarchar(20) NULL,
    [Carrier] nvarchar(30) NULL,
    [CarrierShipmentId] nvarchar(60) NULL,
    [TrackingNo] nvarchar(60) NULL,
    [TrackingUrl] nvarchar(300) NULL,
    [LabelUrl] nvarchar(300) NULL,
    [Parcels] int NOT NULL,
    [WeightKg] decimal(9,3) NOT NULL,
    [PackedBy] nvarchar(100) NULL,
    [CreatedUtc] datetime2 NOT NULL,
    [PackedUtc] datetime2 NULL,
    [DispatchedUtc] datetime2 NULL,
    [DeliveredUtc] datetime2 NULL,
    CONSTRAINT [PK_Shipment] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Shipment_Order_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Order] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ShipmentLine] (
    [Id] bigint NOT NULL IDENTITY,
    [ShipmentId] uniqueidentifier NOT NULL,
    [Sku] nvarchar(40) NULL,
    [Quantity] int NOT NULL,
    CONSTRAINT [PK_ShipmentLine] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ShipmentLine_Shipment_ShipmentId] FOREIGN KEY ([ShipmentId]) REFERENCES [Shipment] ([Id]) ON DELETE CASCADE
);
GO


CREATE UNIQUE INDEX [IX_InboundWebhook_Source_ExternalId] ON [InboundWebhook] ([Source], [ExternalId]) WHERE [Source] IS NOT NULL AND [ExternalId] IS NOT NULL;
GO


CREATE INDEX [IX_Order_CustomerEmail] ON [Order] ([CustomerEmail]);
GO


CREATE UNIQUE INDEX [IX_Order_OrderNo] ON [Order] ([OrderNo]);
GO


CREATE INDEX [IX_Order_Status_PlacedUtc] ON [Order] ([Status], [PlacedUtc]);
GO


CREATE INDEX [IX_OrderEvent_OrderId] ON [OrderEvent] ([OrderId]);
GO


CREATE INDEX [IX_OrderLine_OrderId] ON [OrderLine] ([OrderId]);
GO


CREATE UNIQUE INDEX [IX_OutboxMessage_MessageId] ON [OutboxMessage] ([MessageId]);
GO


CREATE INDEX [IX_OutboxMessage_SentUtc_Id] ON [OutboxMessage] ([SentUtc], [Id]);
GO


CREATE INDEX [IX_Shipment_CarrierShipmentId] ON [Shipment] ([CarrierShipmentId]);
GO


CREATE INDEX [IX_Shipment_OrderId] ON [Shipment] ([OrderId]);
GO


CREATE INDEX [IX_ShipmentLine_ShipmentId] ON [ShipmentLine] ([ShipmentId]);
GO


