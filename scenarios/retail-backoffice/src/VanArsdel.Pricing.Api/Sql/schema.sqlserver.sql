-- Pricing database (VanArsdelPricing). Run by deploy\Initialize-Database.ps1.
IF OBJECT_ID('dbo.SkuAttribute') IS NULL
CREATE TABLE dbo.SkuAttribute (
    Sku            NVARCHAR(40)  NOT NULL CONSTRAINT PK_SkuAttribute PRIMARY KEY,
    StyleCode      NVARCHAR(20)  NOT NULL,
    CategoryCode   NVARCHAR(20)  NOT NULL,
    Brand          NVARCHAR(100) NULL,
    Season         NVARCHAR(10)  NULL,
    Discontinued   BIT           NOT NULL CONSTRAINT DF_SkuAttribute_Discontinued DEFAULT (0),
    UpdatedUtc     DATETIME2     NOT NULL
);
GO
IF OBJECT_ID('dbo.BasePrice') IS NULL
CREATE TABLE dbo.BasePrice (
    Sku            NVARCHAR(40)  NOT NULL,
    Market         CHAR(2)       NOT NULL,
    Currency       CHAR(3)       NOT NULL,
    Price          DECIMAL(18,2) NOT NULL,
    UpdatedUtc     DATETIME2     NOT NULL,
    CONSTRAINT PK_BasePrice PRIMARY KEY (Sku, Market)
);
GO
IF OBJECT_ID('dbo.PriceList') IS NULL
CREATE TABLE dbo.PriceList (
    Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PriceList PRIMARY KEY,
    Name           NVARCHAR(100) NOT NULL,
    Market         CHAR(2)       NOT NULL,
    Currency       CHAR(3)       NOT NULL,
    ValidFrom      DATE          NOT NULL,
    Status         NVARCHAR(20)  NOT NULL,
    CreatedBy      NVARCHAR(100) NULL,
    CreatedUtc     DATETIME2     NOT NULL,
    PublishedUtc   DATETIME2     NULL
);
GO
IF OBJECT_ID('dbo.PriceListItem') IS NULL
CREATE TABLE dbo.PriceListItem (
    PriceListId    INT           NOT NULL CONSTRAINT FK_PriceListItem_PriceList REFERENCES dbo.PriceList (Id) ON DELETE CASCADE,
    Sku            NVARCHAR(40)  NOT NULL,
    Price          DECIMAL(18,2) NOT NULL,
    CompareAtPrice DECIMAL(18,2) NULL,
    CONSTRAINT PK_PriceListItem PRIMARY KEY (PriceListId, Sku)
);
GO
IF OBJECT_ID('dbo.EffectivePrice') IS NULL
CREATE TABLE dbo.EffectivePrice (
    Sku            NVARCHAR(40)  NOT NULL,
    Market         CHAR(2)       NOT NULL,
    Price          DECIMAL(18,2) NOT NULL,
    CompareAtPrice DECIMAL(18,2) NULL,
    PriceListId    INT           NULL,
    ValidFrom      DATE          NOT NULL,
    CONSTRAINT PK_EffectivePrice PRIMARY KEY (Sku, Market)
);
GO
IF OBJECT_ID('dbo.Promotion') IS NULL
CREATE TABLE dbo.Promotion (
    Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Promotion PRIMARY KEY,
    Code           NVARCHAR(30)  NOT NULL CONSTRAINT UQ_Promotion_Code UNIQUE,
    Name           NVARCHAR(200) NOT NULL,
    Type           NVARCHAR(20)  NOT NULL,
    Channel        NVARCHAR(10)  NOT NULL,
    Markets        NVARCHAR(50)  NOT NULL,
    StartsUtc      DATETIME2     NOT NULL,
    EndsUtc        DATETIME2     NOT NULL,
    Priority       INT           NOT NULL,
    Exclusive      BIT           NOT NULL,
    RequiresCode   BIT           NOT NULL,
    Value          DECIMAL(18,2) NOT NULL,
    BuyQuantity    INT           NULL,
    PayQuantity    INT           NULL,
    MinBasket      DECIMAL(18,2) NULL,
    Status         NVARCHAR(20)  NOT NULL,
    CreatedUtc     DATETIME2     NOT NULL
);
GO
IF OBJECT_ID('dbo.PromotionScope') IS NULL
CREATE TABLE dbo.PromotionScope (
    PromotionId    INT           NOT NULL CONSTRAINT FK_PromotionScope_Promotion REFERENCES dbo.Promotion (Id) ON DELETE CASCADE,
    ScopeType      NVARCHAR(20)  NOT NULL,
    Value          NVARCHAR(100) NOT NULL,
    CONSTRAINT PK_PromotionScope PRIMARY KEY (PromotionId, ScopeType, Value)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PriceList_Status_ValidFrom')
CREATE INDEX IX_PriceList_Status_ValidFrom ON dbo.PriceList (Status, ValidFrom);
GO
