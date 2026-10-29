-- sqlcmd -S sql01.corp.local -E -d Purchasing -i 02-schema.sql
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.CostCenters') IS NULL
CREATE TABLE dbo.CostCenters
(
    Code            nvarchar(10)  NOT NULL CONSTRAINT PK_CostCenters PRIMARY KEY,
    Name            nvarchar(100) NOT NULL,
    ManagerAccount  nvarchar(100) NOT NULL,
    ManagerEmail    nvarchar(200) NULL
);
GO

IF OBJECT_ID(N'dbo.Suppliers') IS NULL
CREATE TABLE dbo.Suppliers
(
    Id              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Suppliers PRIMARY KEY,
    Code            nvarchar(20)  NOT NULL CONSTRAINT UQ_Suppliers_Code UNIQUE,
    Name            nvarchar(200) NOT NULL,
    VatNumber       nvarchar(30)  NULL,
    Email           nvarchar(200) NULL,
    ErpVendorNo     nvarchar(10)  NOT NULL,
    Active          bit NOT NULL CONSTRAINT DF_Suppliers_Active DEFAULT (1)
);
GO

IF OBJECT_ID(N'dbo.RequestNumbers') IS NULL
    CREATE SEQUENCE dbo.RequestNumbers AS bigint START WITH 1 INCREMENT BY 1;
GO

IF OBJECT_ID(N'dbo.OrderNumbers') IS NULL
    CREATE SEQUENCE dbo.OrderNumbers AS bigint START WITH 1001 INCREMENT BY 1;
GO

IF OBJECT_ID(N'dbo.PurchaseRequests') IS NULL
CREATE TABLE dbo.PurchaseRequests
(
    Id              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PurchaseRequests PRIMARY KEY,
    Number          nvarchar(20)  NOT NULL CONSTRAINT UQ_PurchaseRequests_Number UNIQUE,
    RequesterAccount nvarchar(100) NOT NULL,
    RequesterName   nvarchar(200) NULL,
    RequesterEmail  nvarchar(200) NULL,
    CostCenterCode  nvarchar(10)  NOT NULL CONSTRAINT FK_PurchaseRequests_CostCenters REFERENCES dbo.CostCenters (Code),
    SupplierId      int NOT NULL CONSTRAINT FK_PurchaseRequests_Suppliers REFERENCES dbo.Suppliers (Id),
    Category        nvarchar(20)  NOT NULL,
    Title           nvarchar(200) NOT NULL,
    Justification   nvarchar(2000) NULL,
    NeededBy        date NULL,
    Currency        char(3) NOT NULL CONSTRAINT DF_PurchaseRequests_Currency DEFAULT ('EUR'),
    Status          tinyint NOT NULL,
    CreatedAt       datetime2(0) NOT NULL,
    SubmittedAt     datetime2(0) NULL,
    LastReminderAt  datetime2(0) NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PurchaseRequests_Requester')
    CREATE INDEX IX_PurchaseRequests_Requester ON dbo.PurchaseRequests (RequesterAccount, CreatedAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PurchaseRequests_Status')
    CREATE INDEX IX_PurchaseRequests_Status ON dbo.PurchaseRequests (Status, SubmittedAt);
GO

IF OBJECT_ID(N'dbo.RequestLines') IS NULL
CREATE TABLE dbo.RequestLines
(
    RequestId       int NOT NULL CONSTRAINT FK_RequestLines_PurchaseRequests REFERENCES dbo.PurchaseRequests (Id) ON DELETE CASCADE,
    [LineNo]        int NOT NULL,
    Description     nvarchar(300) NOT NULL,
    Quantity        decimal(18,3) NOT NULL,
    Unit            nvarchar(10)  NULL,
    UnitPrice       decimal(18,4) NOT NULL,
    VatRate         decimal(5,2)  NOT NULL,
    CONSTRAINT PK_RequestLines PRIMARY KEY (RequestId, [LineNo])
);
GO

IF OBJECT_ID(N'dbo.Approvals') IS NULL
CREATE TABLE dbo.Approvals
(
    Id              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Approvals PRIMARY KEY,
    RequestId       int NOT NULL CONSTRAINT FK_Approvals_PurchaseRequests REFERENCES dbo.PurchaseRequests (Id) ON DELETE CASCADE,
    Step            tinyint NOT NULL,
    ApproverAccount nvarchar(100) NOT NULL,
    ApproverName    nvarchar(200) NULL,
    Decision        tinyint NOT NULL,
    Comment         nvarchar(1000) NULL,
    DecidedAt       datetime2(0) NOT NULL
);
GO

IF OBJECT_ID(N'dbo.Attachments') IS NULL
CREATE TABLE dbo.Attachments
(
    Id              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Attachments PRIMARY KEY,
    RequestId       int NOT NULL CONSTRAINT FK_Attachments_PurchaseRequests REFERENCES dbo.PurchaseRequests (Id) ON DELETE CASCADE,
    FileName        nvarchar(260) NOT NULL,
    StoredName      nvarchar(260) NOT NULL,
    ContentType     nvarchar(100) NOT NULL,
    SizeBytes       bigint NOT NULL,
    UploadedBy      nvarchar(100) NOT NULL,
    UploadedAt      datetime2(0) NOT NULL
);
GO

IF OBJECT_ID(N'dbo.PurchaseOrders') IS NULL
CREATE TABLE dbo.PurchaseOrders
(
    Id              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PurchaseOrders PRIMARY KEY,
    PoNumber        nvarchar(10)  NOT NULL CONSTRAINT UQ_PurchaseOrders_PoNumber UNIQUE,
    RequestId       int NOT NULL CONSTRAINT FK_PurchaseOrders_PurchaseRequests REFERENCES dbo.PurchaseRequests (Id)
                        CONSTRAINT UQ_PurchaseOrders_Request UNIQUE,
    BuyerAccount    nvarchar(100) NOT NULL,
    OrderDate       date NOT NULL,
    DeliveryAddress nvarchar(400) NULL,
    ExportedAt      datetime2(0) NULL,
    ExportFile      nvarchar(100) NULL
);
GO
