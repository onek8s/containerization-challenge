-- Order Hub database schema. Run with sqlcmd -v DatabaseName=OrderHub
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF DB_ID(N'$(DatabaseName)') IS NULL
    EXEC (N'CREATE DATABASE [$(DatabaseName)]');
GO

USE [$(DatabaseName)];
GO

IF SCHEMA_ID(N'hub') IS NULL EXEC (N'CREATE SCHEMA hub AUTHORIZATION dbo');
GO

IF OBJECT_ID(N'hub.OrderNumbers', N'SO') IS NULL
    CREATE SEQUENCE hub.OrderNumbers AS int START WITH 1000001 INCREMENT BY 1;
IF OBJECT_ID(N'hub.InvoiceNumbers', N'SO') IS NULL
    CREATE SEQUENCE hub.InvoiceNumbers AS int START WITH 500001 INCREMENT BY 1;
GO

IF OBJECT_ID(N'hub.Products', N'U') IS NULL
CREATE TABLE hub.Products
(
    Sku           varchar(20)    NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
    Description   nvarchar(100)  NOT NULL,
    Ean           varchar(14)    NULL,
    Active        bit            NOT NULL CONSTRAINT DF_Products_Active DEFAULT (1)
);
GO

IF OBJECT_ID(N'hub.Orders', N'U') IS NULL
CREATE TABLE hub.Orders
(
    OrderId          uniqueidentifier NOT NULL CONSTRAINT PK_Orders PRIMARY KEY NONCLUSTERED,
    OrderNumber      varchar(12)      NULL,
    Channel          varchar(20)      NOT NULL,
    ExternalRef      nvarchar(64)     NOT NULL,
    Status           varchar(20)      NOT NULL,
    StatusReason     nvarchar(1000)   NULL,
    CustomerName     nvarchar(100)    NULL,
    CustomerEmail    nvarchar(200)    NULL,
    Phone            varchar(30)      NULL,
    CustomerReference nvarchar(64)    NULL,
    ShipName         nvarchar(100)    NULL,
    ShipStreet       nvarchar(100)    NULL,
    ShipStreet2      nvarchar(100)    NULL,
    ShipPostalCode   varchar(12)      NULL,
    ShipCity         nvarchar(60)     NULL,
    ShipCountry      char(2)          NULL,
    Currency         char(3)          NULL,
    ShippingMethod   varchar(30)      NULL,
    ShippingAmount   decimal(12,2)    NOT NULL CONSTRAINT DF_Orders_Shipping DEFAULT (0),
    ReceivedUtc      datetime2(0)     NOT NULL,
    UpdatedUtc       datetime2(0)     NOT NULL,
    WarehouseFile    varchar(60)      NULL,
    Carrier          varchar(30)      NULL,
    TrackingNumber   varchar(40)      NULL,
    ShippedUtc       datetime2(0)     NULL,
    InvoiceNumber    varchar(12)      NULL,
    InvoicedUtc      datetime2(0)     NULL,
    CONSTRAINT UQ_Orders_ChannelRef UNIQUE (Channel, ExternalRef)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Orders_OrderNumber')
    CREATE UNIQUE INDEX UX_Orders_OrderNumber ON hub.Orders (OrderNumber) WHERE OrderNumber IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'CX_Orders_Received')
    CREATE CLUSTERED INDEX CX_Orders_Received ON hub.Orders (ReceivedUtc);
GO

IF OBJECT_ID(N'hub.OrderLines', N'U') IS NULL
CREATE TABLE hub.OrderLines
(
    OrderId          uniqueidentifier NOT NULL CONSTRAINT FK_OrderLines_Orders REFERENCES hub.Orders (OrderId),
    LineNumber       int              NOT NULL,
    Sku              varchar(20)      NULL,
    Description      nvarchar(200)    NULL,
    Quantity         int              NOT NULL,
    UnitPrice        decimal(12,2)    NOT NULL,
    ShippedQuantity  int              NULL,
    CONSTRAINT PK_OrderLines PRIMARY KEY (OrderId, LineNumber)
);
GO

IF OBJECT_ID(N'hub.OrderEvents', N'U') IS NULL
CREATE TABLE hub.OrderEvents
(
    EventId    bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderEvents PRIMARY KEY,
    OrderId    uniqueidentifier NOT NULL,
    AtUtc      datetime2(3)     NOT NULL,
    EventType  varchar(40)      NOT NULL,
    Detail     nvarchar(2000)   NULL
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OrderEvents_Order')
    CREATE INDEX IX_OrderEvents_Order ON hub.OrderEvents (OrderId, AtUtc);
GO

IF OBJECT_ID(N'hub.InvoiceDocuments', N'U') IS NULL
CREATE TABLE hub.InvoiceDocuments
(
    InvoiceNumber  varchar(12)      NOT NULL CONSTRAINT PK_InvoiceDocuments PRIMARY KEY,
    OrderId        uniqueidentifier NOT NULL,
    CreatedUtc     datetime2(0)     NOT NULL,
    Pdf            varbinary(max)   NOT NULL
);
GO

IF OBJECT_ID(N'hub.ReportFiles', N'U') IS NULL
CREATE TABLE hub.ReportFiles
(
    ReportFileId   int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReportFiles PRIMARY KEY,
    FileName       nvarchar(100)    NOT NULL CONSTRAINT UQ_ReportFiles_Name UNIQUE,
    ReportDate     date             NOT NULL,
    CreatedUtc     datetime2(0)     NOT NULL,
    Content        varbinary(max)   NOT NULL
);
GO

-- Read model for the invoice report (SSRS data set) and the daily sales workbook.
CREATE OR ALTER VIEW hub.InvoiceLines
AS
SELECT o.OrderId, o.OrderNumber, o.InvoiceNumber, o.InvoicedUtc, o.ShippedUtc, o.Channel, o.CustomerReference,
       o.CustomerName, o.CustomerEmail, o.ShipName, o.ShipStreet, o.ShipStreet2, o.ShipPostalCode, o.ShipCity, o.ShipCountry,
       o.Currency, o.ShippingAmount, o.Carrier, o.TrackingNumber,
       l.LineNumber, l.Sku, l.Description, COALESCE(l.ShippedQuantity, l.Quantity) AS Quantity, l.UnitPrice,
       CAST(COALESCE(l.ShippedQuantity, l.Quantity) * l.UnitPrice AS decimal(12,2)) AS LineAmount
FROM hub.Orders o
JOIN hub.OrderLines l ON l.OrderId = o.OrderId;
GO

-- ASP.NET Core Identity 2.1 tables used by the portal.
IF OBJECT_ID(N'dbo.AspNetRoles', N'U') IS NULL
CREATE TABLE dbo.AspNetRoles
(
    Id               nvarchar(450) NOT NULL CONSTRAINT PK_AspNetRoles PRIMARY KEY,
    Name             nvarchar(256) NULL,
    NormalizedName   nvarchar(256) NULL,
    ConcurrencyStamp nvarchar(max) NULL
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'RoleNameIndex')
    CREATE UNIQUE INDEX RoleNameIndex ON dbo.AspNetRoles (NormalizedName) WHERE NormalizedName IS NOT NULL;
GO

IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NULL
CREATE TABLE dbo.AspNetUsers
(
    Id                   nvarchar(450)  NOT NULL CONSTRAINT PK_AspNetUsers PRIMARY KEY,
    UserName             nvarchar(256)  NULL,
    NormalizedUserName   nvarchar(256)  NULL,
    Email                nvarchar(256)  NULL,
    NormalizedEmail      nvarchar(256)  NULL,
    EmailConfirmed       bit            NOT NULL,
    PasswordHash         nvarchar(max)  NULL,
    SecurityStamp        nvarchar(max)  NULL,
    ConcurrencyStamp     nvarchar(max)  NULL,
    PhoneNumber          nvarchar(max)  NULL,
    PhoneNumberConfirmed bit            NOT NULL,
    TwoFactorEnabled     bit            NOT NULL,
    LockoutEnd           datetimeoffset NULL,
    LockoutEnabled       bit            NOT NULL,
    AccessFailedCount    int            NOT NULL
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UserNameIndex')
    CREATE UNIQUE INDEX UserNameIndex ON dbo.AspNetUsers (NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'EmailIndex')
    CREATE INDEX EmailIndex ON dbo.AspNetUsers (NormalizedEmail);
GO

IF OBJECT_ID(N'dbo.AspNetRoleClaims', N'U') IS NULL
CREATE TABLE dbo.AspNetRoleClaims
(
    Id         int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AspNetRoleClaims PRIMARY KEY,
    RoleId     nvarchar(450) NOT NULL CONSTRAINT FK_AspNetRoleClaims_AspNetRoles_RoleId REFERENCES dbo.AspNetRoles (Id) ON DELETE CASCADE,
    ClaimType  nvarchar(max) NULL,
    ClaimValue nvarchar(max) NULL
);
IF OBJECT_ID(N'dbo.AspNetUserClaims', N'U') IS NULL
CREATE TABLE dbo.AspNetUserClaims
(
    Id         int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AspNetUserClaims PRIMARY KEY,
    UserId     nvarchar(450) NOT NULL CONSTRAINT FK_AspNetUserClaims_AspNetUsers_UserId REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE,
    ClaimType  nvarchar(max) NULL,
    ClaimValue nvarchar(max) NULL
);
IF OBJECT_ID(N'dbo.AspNetUserLogins', N'U') IS NULL
CREATE TABLE dbo.AspNetUserLogins
(
    LoginProvider       nvarchar(128) NOT NULL,
    ProviderKey         nvarchar(128) NOT NULL,
    ProviderDisplayName nvarchar(max) NULL,
    UserId              nvarchar(450) NOT NULL CONSTRAINT FK_AspNetUserLogins_AspNetUsers_UserId REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE,
    CONSTRAINT PK_AspNetUserLogins PRIMARY KEY (LoginProvider, ProviderKey)
);
IF OBJECT_ID(N'dbo.AspNetUserRoles', N'U') IS NULL
CREATE TABLE dbo.AspNetUserRoles
(
    UserId nvarchar(450) NOT NULL CONSTRAINT FK_AspNetUserRoles_AspNetUsers_UserId REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE,
    RoleId nvarchar(450) NOT NULL CONSTRAINT FK_AspNetUserRoles_AspNetRoles_RoleId REFERENCES dbo.AspNetRoles (Id) ON DELETE CASCADE,
    CONSTRAINT PK_AspNetUserRoles PRIMARY KEY (UserId, RoleId)
);
IF OBJECT_ID(N'dbo.AspNetUserTokens', N'U') IS NULL
CREATE TABLE dbo.AspNetUserTokens
(
    UserId        nvarchar(450) NOT NULL CONSTRAINT FK_AspNetUserTokens_AspNetUsers_UserId REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE,
    LoginProvider nvarchar(128) NOT NULL,
    Name          nvarchar(128) NOT NULL,
    Value         nvarchar(max) NULL,
    CONSTRAINT PK_AspNetUserTokens PRIMARY KEY (UserId, LoginProvider, Name)
);
GO
