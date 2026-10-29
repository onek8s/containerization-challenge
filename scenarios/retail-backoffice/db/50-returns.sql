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


CREATE TABLE [ReturnRequest] (
    [Id] uniqueidentifier NOT NULL,
    [RmaNo] nvarchar(20) NULL,
    [OrderNo] nvarchar(30) NULL,
    [OrderId] uniqueidentifier NULL,
    [Channel] nvarchar(10) NULL,
    [StoreCode] nvarchar(10) NULL,
    [CustomerEmail] nvarchar(200) NULL,
    [LoyaltyCardNo] nvarchar(20) NULL,
    [Market] nvarchar(2) NULL,
    [Currency] nvarchar(3) NULL,
    [Status] nvarchar(20) NULL,
    [PaymentReference] nvarchar(100) NULL,
    [PaymentMethod] nvarchar(30) NULL,
    [RefundMethod] nvarchar(20) NULL,
    [RefundAmount] decimal(18,2) NOT NULL,
    [RefundReference] nvarchar(100) NULL,
    [ReturnTrackingNo] nvarchar(60) NULL,
    [ReturnLabelUrl] nvarchar(300) NULL,
    [ReceivedAt] nvarchar(10) NULL,
    [RejectReason] nvarchar(300) NULL,
    [HandledBy] nvarchar(100) NULL,
    [OrderPlacedUtc] datetime2 NOT NULL,
    [CreatedUtc] datetime2 NOT NULL,
    [ReceivedUtc] datetime2 NULL,
    [CompletedUtc] datetime2 NULL,
    CONSTRAINT [PK_ReturnRequest] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [ReturnLine] (
    [Id] bigint NOT NULL IDENTITY,
    [ReturnId] uniqueidentifier NOT NULL,
    [Sku] nvarchar(40) NULL,
    [Quantity] int NOT NULL,
    [ReceivedQuantity] int NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [ReasonCode] nvarchar(20) NULL,
    [Grade] nvarchar(1) NULL,
    CONSTRAINT [PK_ReturnLine] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReturnLine_ReturnRequest_ReturnId] FOREIGN KEY ([ReturnId]) REFERENCES [ReturnRequest] ([Id]) ON DELETE CASCADE
);
GO


CREATE UNIQUE INDEX [IX_OutboxMessage_MessageId] ON [OutboxMessage] ([MessageId]);
GO


CREATE INDEX [IX_OutboxMessage_SentUtc_Id] ON [OutboxMessage] ([SentUtc], [Id]);
GO


CREATE INDEX [IX_ReturnLine_ReturnId] ON [ReturnLine] ([ReturnId]);
GO


CREATE INDEX [IX_ReturnRequest_OrderNo] ON [ReturnRequest] ([OrderNo]);
GO


CREATE INDEX [IX_ReturnRequest_ReturnTrackingNo] ON [ReturnRequest] ([ReturnTrackingNo]);
GO


CREATE UNIQUE INDEX [IX_ReturnRequest_RmaNo] ON [ReturnRequest] ([RmaNo]) WHERE [RmaNo] IS NOT NULL;
GO


