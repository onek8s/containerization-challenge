CREATE TABLE [Brand] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(10) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [OwnBrand] bit NOT NULL,
    CONSTRAINT [PK_Brand] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Category] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(20) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [ParentId] int NULL,
    [ErpItemGroup] nvarchar(20) NULL,
    [SizeScale] nvarchar(20) NULL,
    CONSTRAINT [PK_Category] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Category_Category_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [Category] ([Id]) ON DELETE NO ACTION
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


CREATE TABLE [Product] (
    [Id] uniqueidentifier NOT NULL,
    [StyleCode] nvarchar(20) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(4000) NULL,
    [BrandId] int NOT NULL,
    [CategoryId] int NOT NULL,
    [Season] nvarchar(10) NULL,
    [Gender] nvarchar(10) NULL,
    [Material] nvarchar(200) NULL,
    [CareInstructions] nvarchar(500) NULL,
    [VatClass] nvarchar(20) NULL,
    [RecommendedPriceEur] decimal(18,2) NOT NULL,
    [RecommendedPriceSek] decimal(18,2) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [CreatedUtc] datetime2 NOT NULL,
    [UpdatedUtc] datetime2 NOT NULL,
    [PublishedUtc] datetime2 NULL,
    [DiscontinuedOn] datetime2 NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_Product] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Product_Brand_BrandId] FOREIGN KEY ([BrandId]) REFERENCES [Brand] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Product_Category_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Category] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [ProductImage] (
    [Id] int NOT NULL IDENTITY,
    [ProductId] uniqueidentifier NOT NULL,
    [ColourCode] nvarchar(10) NULL,
    [Url] nvarchar(500) NOT NULL,
    [SortOrder] int NOT NULL,
    CONSTRAINT [PK_ProductImage] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductImage_Product_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Product] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Sku] (
    [Id] int NOT NULL IDENTITY,
    [ProductId] uniqueidentifier NOT NULL,
    [SkuCode] nvarchar(40) NOT NULL,
    [Ean] nvarchar(13) NULL,
    [ColourCode] nvarchar(10) NULL,
    [ColourName] nvarchar(50) NULL,
    [Size] nvarchar(10) NULL,
    [SortOrder] int NOT NULL,
    [CostPrice] decimal(18,2) NOT NULL,
    [WeightKg] decimal(9,3) NOT NULL,
    [Blocked] bit NOT NULL,
    CONSTRAINT [PK_Sku] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Sku_Product_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Product] ([Id]) ON DELETE CASCADE
);
GO


CREATE UNIQUE INDEX [IX_Brand_Code] ON [Brand] ([Code]);
GO


CREATE UNIQUE INDEX [IX_Category_Code] ON [Category] ([Code]);
GO


CREATE INDEX [IX_Category_ParentId] ON [Category] ([ParentId]);
GO


CREATE UNIQUE INDEX [IX_OutboxMessage_MessageId] ON [OutboxMessage] ([MessageId]);
GO


CREATE INDEX [IX_OutboxMessage_SentUtc_Id] ON [OutboxMessage] ([SentUtc], [Id]);
GO


CREATE INDEX [IX_Product_BrandId] ON [Product] ([BrandId]);
GO


CREATE INDEX [IX_Product_CategoryId] ON [Product] ([CategoryId]);
GO


CREATE UNIQUE INDEX [IX_Product_StyleCode] ON [Product] ([StyleCode]);
GO


CREATE INDEX [IX_ProductImage_ProductId] ON [ProductImage] ([ProductId]);
GO


CREATE INDEX [IX_Sku_Ean] ON [Sku] ([Ean]);
GO


CREATE INDEX [IX_Sku_ProductId] ON [Sku] ([ProductId]);
GO


CREATE UNIQUE INDEX [IX_Sku_SkuCode] ON [Sku] ([SkuCode]);
GO


