CREATE TABLE [ErpPosting] (
    [Id] bigint NOT NULL IDENTITY,
    [Kind] nvarchar(30) NOT NULL,
    [ExternalReference] nvarchar(60) NOT NULL,
    [DocumentNo] nvarchar(30) NULL,
    [Status] nvarchar(10) NOT NULL,
    [Attempts] int NOT NULL,
    [Error] nvarchar(1000) NULL,
    [CreatedUtc] datetime2 NOT NULL,
    [UpdatedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ErpPosting] PRIMARY KEY ([Id])
);
GO


CREATE UNIQUE INDEX [IX_ErpPosting_Kind_ExternalReference] ON [ErpPosting] ([Kind], [ExternalReference]);
GO


CREATE INDEX [IX_ErpPosting_Status] ON [ErpPosting] ([Status]);
GO


