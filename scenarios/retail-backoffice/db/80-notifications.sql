CREATE TABLE [SentNotification] (
    [Id] bigint NOT NULL IDENTITY,
    [DedupeKey] nvarchar(200) NULL,
    [Template] nvarchar(50) NULL,
    [Channel] nvarchar(10) NULL,
    [Reference] nvarchar(60) NULL,
    [RecipientHash] nvarchar(64) NULL,
    [Provider] nvarchar(20) NULL,
    [ProviderMessageId] nvarchar(100) NULL,
    [Status] nvarchar(10) NULL,
    [Error] nvarchar(1000) NULL,
    [SentUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_SentNotification] PRIMARY KEY ([Id])
);
GO


CREATE UNIQUE INDEX [IX_SentNotification_DedupeKey] ON [SentNotification] ([DedupeKey]) WHERE [DedupeKey] IS NOT NULL;
GO


CREATE INDEX [IX_SentNotification_RecipientHash] ON [SentNotification] ([RecipientHash]);
GO


CREATE INDEX [IX_SentNotification_Reference] ON [SentNotification] ([Reference]);
GO


