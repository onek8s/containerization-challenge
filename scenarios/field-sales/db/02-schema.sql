-- Tables, types and the order number sequence. Safe to run once on an empty database.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE SEQUENCE dbo.SalesOrderNumber AS int START WITH 100001 INCREMENT BY 1;
GO

CREATE TABLE dbo.CatalogState
(
    Id               int    NOT NULL CONSTRAINT PK_CatalogState PRIMARY KEY CHECK (Id = 1),
    PriceListVersion bigint NOT NULL
);
INSERT dbo.CatalogState (Id, PriceListVersion) VALUES (1, 1);
GO

CREATE TABLE dbo.Customer
(
    CustomerId    int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customer PRIMARY KEY,
    CustomerNo    nvarchar(20)  NOT NULL CONSTRAINT UQ_Customer_No UNIQUE,
    Name          nvarchar(120) NOT NULL,
    Street        nvarchar(120) NULL,
    PostalCode    nvarchar(10)  NULL,
    City          nvarchar(60)  NULL,
    Email         nvarchar(200) NULL,
    PriceGroup    nchar(1)      NOT NULL CONSTRAINT DF_Customer_PriceGroup DEFAULT (N'C'),
    RepCode       nvarchar(4)   NULL,
    DeliveryDays  int           NOT NULL CONSTRAINT DF_Customer_DeliveryDays DEFAULT (0),
    CreditBlocked bit           NOT NULL CONSTRAINT DF_Customer_CreditBlocked DEFAULT (0),
    ModifiedAt    datetime      NOT NULL CONSTRAINT DF_Customer_ModifiedAt DEFAULT (GETDATE())
);
CREATE INDEX IX_Customer_RepCode ON dbo.Customer (RepCode) INCLUDE (Name);
GO

CREATE TABLE dbo.Product
(
    ProductId   int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Product PRIMARY KEY,
    ItemNo      nvarchar(20)  NOT NULL CONSTRAINT UQ_Product_ItemNo UNIQUE,
    Name        nvarchar(120) NOT NULL,
    Category    nvarchar(10)  NOT NULL,
    Unit        nchar(2)      NOT NULL CONSTRAINT CK_Product_Unit CHECK (Unit IN (N'EA', N'CS')),
    CaseSize    int           NOT NULL,
    ListPrice   decimal(12,2) NOT NULL,
    VatRate     decimal(5,2)  NOT NULL,
    MinOrderQty int           NOT NULL CONSTRAINT DF_Product_MinOrderQty DEFAULT (1),
    Active      bit           NOT NULL CONSTRAINT DF_Product_Active DEFAULT (1),
    RowVer      rowversion    NOT NULL
);
CREATE INDEX IX_Product_RowVer ON dbo.Product (RowVer);
GO

CREATE TABLE dbo.PriceRule
(
    PriceRuleId     int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PriceRule PRIMARY KEY,
    PriceGroup      nchar(1)     NOT NULL,
    Category        nvarchar(10) NULL,
    DiscountPercent decimal(5,2) NOT NULL,
    CONSTRAINT UQ_PriceRule UNIQUE (PriceGroup, Category)
);
GO

CREATE TABLE dbo.Promotion
(
    PromotionId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Promotion PRIMARY KEY,
    ItemNo      nvarchar(20)  NOT NULL,
    PromoPrice  decimal(12,2) NOT NULL,
    ValidFrom   datetime      NOT NULL,
    ValidTo     datetime      NOT NULL,
    CreatedBy   nvarchar(256) NOT NULL
);
CREATE INDEX IX_Promotion_ItemNo ON dbo.Promotion (ItemNo, ValidTo);
GO

CREATE TABLE dbo.Holiday
(
    Day  datetime     NOT NULL CONSTRAINT PK_Holiday PRIMARY KEY,
    Name nvarchar(60) NOT NULL
);
GO

CREATE TABLE dbo.SalesOrder
(
    SalesOrderId          int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalesOrder PRIMARY KEY,
    OrderNo               nvarchar(20)     NOT NULL CONSTRAINT UQ_SalesOrder_OrderNo UNIQUE,
    ClientOrderId         uniqueidentifier NOT NULL CONSTRAINT UQ_SalesOrder_ClientOrderId UNIQUE,
    RepReference          nvarchar(20)     NULL,
    CustomerId            int              NOT NULL CONSTRAINT FK_SalesOrder_Customer REFERENCES dbo.Customer (CustomerId),
    CustomerNo            nvarchar(20)     NOT NULL,
    RepCode               nvarchar(4)      NULL,
    SubmittedBy           nvarchar(256)    NOT NULL,
    CreatedAt             datetime         NOT NULL,
    ReceivedAt            datetime         NOT NULL CONSTRAINT DF_SalesOrder_ReceivedAt DEFAULT (GETDATE()),
    RequestedDelivery     datetime         NOT NULL,
    Status                int              NOT NULL,
    NetTotal              decimal(14,2)    NOT NULL,
    VatTotal              decimal(14,2)    NOT NULL,
    ManualDiscountPercent decimal(5,2)     NOT NULL,
    Comment               nvarchar(500)    NULL,
    DecidedBy             nvarchar(256)    NULL,
    DecidedAt             datetime         NULL,
    DecisionComment       nvarchar(400)    NULL,
    ConfirmationSentAt    datetime         NULL,
    ErpOrderNo            nvarchar(20)     NULL,
    ExportedAt            datetime         NULL,
    ClientVersion         nvarchar(30)     NULL
);
CREATE INDEX IX_SalesOrder_Status ON dbo.SalesOrder (Status, ExportedAt) INCLUDE (ReceivedAt);
CREATE INDEX IX_SalesOrder_ReceivedAt ON dbo.SalesOrder (ReceivedAt) INCLUDE (RepCode, SubmittedBy, NetTotal, Status);
GO

CREATE TABLE dbo.SalesOrderLine
(
    SalesOrderId        int           NOT NULL CONSTRAINT FK_SalesOrderLine_SalesOrder REFERENCES dbo.SalesOrder (SalesOrderId),
    [LineNo]            int           NOT NULL,
    ItemNo              nvarchar(20)  NOT NULL,
    ItemName            nvarchar(120) NOT NULL,
    Quantity            int           NOT NULL,
    UnitPrice           decimal(12,2) NOT NULL,
    LineDiscountPercent decimal(5,2)  NOT NULL,
    VatRate             decimal(5,2)  NOT NULL,
    LineTotal           decimal(14,2) NOT NULL,
    CONSTRAINT PK_SalesOrderLine PRIMARY KEY (SalesOrderId, [LineNo])
);
GO

CREATE TABLE dbo.RefreshToken
(
    TokenHash       nvarchar(128)  NOT NULL CONSTRAINT PK_RefreshToken PRIMARY KEY,
    UserName        nvarchar(256)  NOT NULL,
    ClientId        nvarchar(50)   NOT NULL,
    IssuedUtc       datetime       NOT NULL,
    ExpiresUtc      datetime       NOT NULL,
    ProtectedTicket nvarchar(max)  NOT NULL
);
CREATE INDEX IX_RefreshToken_UserName ON dbo.RefreshToken (UserName, ExpiresUtc);
GO

CREATE TABLE dbo.PriceListImport
(
    PriceListImportId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PriceListImport PRIMARY KEY,
    FileName          nvarchar(260) NOT NULL,
    ImportedBy        nvarchar(256) NOT NULL,
    ImportedAt        datetime      NOT NULL CONSTRAINT DF_PriceListImport_ImportedAt DEFAULT (GETDATE()),
    Inserted          int           NOT NULL,
    Updated           int           NOT NULL,
    PriceListVersion  bigint        NOT NULL
);
GO

-- ASP.NET Identity 2 tables (same layout the Identity EntityFramework package creates), with the Field Sales
-- profile columns on AspNetUsers.
CREATE TABLE dbo.AspNetRoles
(
    Id   nvarchar(128) NOT NULL CONSTRAINT PK_AspNetRoles PRIMARY KEY,
    Name nvarchar(256) NOT NULL
);
CREATE UNIQUE INDEX RoleNameIndex ON dbo.AspNetRoles (Name);

CREATE TABLE dbo.AspNetUsers
(
    Id                   nvarchar(128) NOT NULL CONSTRAINT PK_AspNetUsers PRIMARY KEY,
    DisplayName          nvarchar(max) NULL,
    RepCode              nvarchar(max) NULL,
    DiscountLimitPercent decimal(18,2) NOT NULL CONSTRAINT DF_AspNetUsers_DiscountLimit DEFAULT (0),
    Email                nvarchar(256) NULL,
    EmailConfirmed       bit NOT NULL,
    PasswordHash         nvarchar(max) NULL,
    SecurityStamp        nvarchar(max) NULL,
    PhoneNumber          nvarchar(max) NULL,
    PhoneNumberConfirmed bit NOT NULL,
    TwoFactorEnabled     bit NOT NULL,
    LockoutEndDateUtc    datetime NULL,
    LockoutEnabled       bit NOT NULL,
    AccessFailedCount    int NOT NULL,
    UserName             nvarchar(256) NOT NULL
);
CREATE UNIQUE INDEX UserNameIndex ON dbo.AspNetUsers (UserName);

CREATE TABLE dbo.AspNetUserRoles
(
    UserId nvarchar(128) NOT NULL CONSTRAINT FK_AspNetUserRoles_Users REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE,
    RoleId nvarchar(128) NOT NULL CONSTRAINT FK_AspNetUserRoles_Roles REFERENCES dbo.AspNetRoles (Id) ON DELETE CASCADE,
    CONSTRAINT PK_AspNetUserRoles PRIMARY KEY (UserId, RoleId)
);

CREATE TABLE dbo.AspNetUserClaims
(
    Id         int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AspNetUserClaims PRIMARY KEY,
    UserId     nvarchar(128) NOT NULL CONSTRAINT FK_AspNetUserClaims_Users REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE,
    ClaimType  nvarchar(max) NULL,
    ClaimValue nvarchar(max) NULL
);

CREATE TABLE dbo.AspNetUserLogins
(
    LoginProvider nvarchar(128) NOT NULL,
    ProviderKey   nvarchar(128) NOT NULL,
    UserId        nvarchar(128) NOT NULL CONSTRAINT FK_AspNetUserLogins_Users REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE,
    CONSTRAINT PK_AspNetUserLogins PRIMARY KEY (LoginProvider, ProviderKey, UserId)
);
GO

CREATE TYPE dbo.OrderLineType AS TABLE
(
    [LineNo]            int           NOT NULL PRIMARY KEY,
    ItemNo              nvarchar(20)  NOT NULL,
    ItemName            nvarchar(120) NOT NULL,
    Quantity            int           NOT NULL,
    UnitPrice           decimal(12,2) NOT NULL,
    LineDiscountPercent decimal(5,2)  NOT NULL,
    VatRate             decimal(5,2)  NOT NULL,
    LineTotal           decimal(14,2) NOT NULL
);

CREATE TYPE dbo.PriceListRowType AS TABLE
(
    ItemNo      nvarchar(20)  NOT NULL PRIMARY KEY,
    Name        nvarchar(120) NOT NULL,
    Category    nvarchar(10)  NOT NULL,
    Unit        nchar(2)      NOT NULL,
    CaseSize    int           NOT NULL,
    ListPrice   decimal(12,2) NOT NULL,
    VatRate     decimal(5,2)  NOT NULL,
    MinOrderQty int           NOT NULL,
    Active      bit           NOT NULL
);

CREATE TYPE dbo.CustomerRowType AS TABLE
(
    CustomerNo    nvarchar(20)  NOT NULL PRIMARY KEY,
    Name          nvarchar(120) NOT NULL,
    Street        nvarchar(120) NULL,
    PostalCode    nvarchar(10)  NULL,
    City          nvarchar(60)  NULL,
    PriceGroup    nchar(1)      NOT NULL,
    CreditBlocked bit           NOT NULL
);
GO
