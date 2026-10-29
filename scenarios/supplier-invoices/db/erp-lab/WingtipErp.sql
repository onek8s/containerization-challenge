-- Test copy of the ERP purchasing and accounts payable tables for the lab and test environments.
-- Production reads the real ERP database on erp01 through the read-only reporting login.
--   sqlcmd -S erp01.corp.local -E -i WingtipErp.sql -v ErpLinkAccount="CORP\svc-inv-erp"
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO
IF DB_ID(N'WingtipErp') IS NULL CREATE DATABASE WingtipErp;
GO
USE WingtipErp;
GO
IF SCHEMA_ID(N'erp') IS NULL EXEC (N'CREATE SCHEMA erp AUTHORIZATION dbo');
GO

IF OBJECT_ID(N'erp.Vendor') IS NULL
CREATE TABLE erp.Vendor
(
    Company            char(4)       NOT NULL,
    VendorNo           varchar(10)   NOT NULL,
    Name               nvarchar(100) NOT NULL,
    VatRegNo           varchar(20)   NULL,
    VatRegNoNormalized varchar(20)   NULL,
    CurrencyCode       char(3)       NOT NULL,
    PaymentTerms       varchar(6)    NULL,
    Blocked            bit           NOT NULL DEFAULT (0),
    CONSTRAINT PK_Vendor PRIMARY KEY (Company, VendorNo)
);
GO
IF OBJECT_ID(N'erp.PurchaseOrder') IS NULL
CREATE TABLE erp.PurchaseOrder
(
    Company      char(4)     NOT NULL,
    PoNo         varchar(10) NOT NULL,
    VendorNo     varchar(10) NOT NULL,
    OrderDate    date        NOT NULL,
    CurrencyCode char(3)     NOT NULL,
    BuyerId      varchar(10) NULL,
    CostCenter   varchar(6)  NULL,
    Status       char(1)     NOT NULL, -- O open, C closed
    CONSTRAINT PK_PurchaseOrder PRIMARY KEY (Company, PoNo)
);
GO
IF OBJECT_ID(N'erp.PurchaseOrderLine') IS NULL
CREATE TABLE erp.PurchaseOrderLine
(
    Company      char(4)       NOT NULL,
    PoNo         varchar(10)   NOT NULL,
    [LineNo]       int           NOT NULL,
    ItemNo       varchar(30)   NULL,
    VendorItemNo varchar(35)   NULL,
    Description  nvarchar(200) NULL,
    OrderedQty   decimal(18,3) NOT NULL,
    ReceivedQty  decimal(18,3) NOT NULL DEFAULT (0),
    InvoicedQty  decimal(18,3) NOT NULL DEFAULT (0),
    UnitPrice    decimal(19,4) NOT NULL,
    PriceUnit    decimal(9,0)  NOT NULL DEFAULT (1),
    GlAccount    varchar(8)    NULL,
    LineType     char(1)       NOT NULL, -- I inventory, E expense, S service
    Cancelled    bit           NOT NULL DEFAULT (0),
    CONSTRAINT PK_PurchaseOrderLine PRIMARY KEY (Company, PoNo, [LineNo])
);
GO
IF OBJECT_ID(N'erp.ApPeriod') IS NULL
CREATE TABLE erp.ApPeriod
(
    Company     char(4) NOT NULL,
    PeriodStart date    NOT NULL,
    PeriodEnd   date    NOT NULL,
    Closed      bit     NOT NULL,
    CONSTRAINT PK_ApPeriod PRIMARY KEY (Company, PeriodStart)
);
GO

DELETE erp.PurchaseOrderLine WHERE Company = 'WT01';
DELETE erp.PurchaseOrder WHERE Company = 'WT01';
DELETE erp.Vendor WHERE Company = 'WT01';
DELETE erp.ApPeriod WHERE Company = 'WT01';

INSERT erp.Vendor (Company, VendorNo, Name, VatRegNo, VatRegNoNormalized, CurrencyCode, PaymentTerms, Blocked) VALUES
    ('WT01', 'V10021', N'Nordic Fasteners Oy',        'FI12345671',   'FI12345671',  'EUR', 'N30',   0),
    ('WT01', 'V10045', N'Baltic Packaging AS',        'EE100234567',  'EE100234567', 'EUR', 'EOM30', 0),
    ('WT01', 'V10107', N'Lakeside Office Supply Ltd', '2345678-2',    'FI23456782',  'EUR', 'N14',   0),
    ('WT01', 'V10230', N'Kuljetus Tuominen Oy',       '3456789-3',    'FI34567893',  'EUR', 'N21',   0),
    ('WT01', 'V10388', N'Securitas Guarding Finland', 'FI45678904',   'FI45678904',  'EUR', 'N30',   0);

INSERT erp.PurchaseOrder (Company, PoNo, VendorNo, OrderDate, CurrencyCode, BuyerId, CostCenter, Status) VALUES
    ('WT01', '4500012001', 'V10021', DATEADD(day, -20, CAST(GETDATE() AS date)), 'EUR', 'MKORHONEN', 'WH01', 'O'),
    ('WT01', '4500012002', 'V10045', DATEADD(day, -18, CAST(GETDATE() AS date)), 'EUR', 'MKORHONEN', 'WH01', 'O'),
    ('WT01', '4500012003', 'V10107', DATEADD(day, -12, CAST(GETDATE() AS date)), 'EUR', 'AVIRTANEN', 'ADM1', 'O'),
    ('WT01', '4500012010', 'V10230', DATEADD(day, -40, CAST(GETDATE() AS date)), 'EUR', 'JLAINE',    'LOG1', 'O'),
    ('WT01', '4500011877', 'V10021', DATEADD(day, -95, CAST(GETDATE() AS date)), 'EUR', 'MKORHONEN', 'WH01', 'C');

INSERT erp.PurchaseOrderLine (Company, PoNo, [LineNo], ItemNo, VendorItemNo, Description, OrderedQty, ReceivedQty, InvoicedQty, UnitPrice, PriceUnit, GlAccount, LineType) VALUES
    ('WT01', '4500012001', 1, '100-2040', 'NF-M840Z',   N'Hex bolt M8x40 zinc plated',     2000, 2000, 0,    8.50,  100, '14100', 'I'),
    ('WT01', '4500012001', 2, '100-2050', 'NF-M8N',     N'Hex nut M8 DIN 934',             2000, 1500, 0,    3.20,  100, '14100', 'I'),
    ('WT01', '4500012002', 1, 'PK-3320',  'BP-6440-DW', N'Carton 600x400x400 double wall', 500,  500,  0,    1.42,  1,   '14200', 'I'),
    ('WT01', '4500012002', 2, 'PK-1010',  'BP-SF500',   N'Stretch film 500 mm',            40,   40,   0,    12.90, 1,   '14200', 'I'),
    ('WT01', '4500012003', 1, 'OF-0001',  NULL,         N'Copy paper A4 80 g',             100,  0,    0,    4.35,  1,   '43100', 'E'),
    ('WT01', '4500012003', 2, 'OF-0420',  NULL,         N'Toner HP 59A',                   4,    0,    0,    89.00, 1,   '43100', 'E'),
    ('WT01', '4500012010', 1, 'FR-LTL',   NULL,         N'Domestic LTL freight, monthly',  12,   0,    9,    1850.00, 1, '47200', 'S'),
    ('WT01', '4500011877', 1, '100-2040', 'NF-M840Z',   N'Hex bolt M8x40 zinc plated',     5000, 5000, 5000, 8.40,  100, '14100', 'I');

DECLARE @month date = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);
INSERT erp.ApPeriod (Company, PeriodStart, PeriodEnd, Closed) VALUES
    ('WT01', DATEADD(month, -2, @month), EOMONTH(DATEADD(month, -2, @month)), 1),
    ('WT01', DATEADD(month, -1, @month), EOMONTH(DATEADD(month, -1, @month)), 0),
    ('WT01', @month, EOMONTH(@month), 0);
GO

IF SUSER_ID(N'$(ErpLinkAccount)') IS NULL CREATE LOGIN [$(ErpLinkAccount)] FROM WINDOWS;
IF DATABASE_PRINCIPAL_ID(N'$(ErpLinkAccount)') IS NULL CREATE USER [$(ErpLinkAccount)] FOR LOGIN [$(ErpLinkAccount)];
ALTER ROLE db_datareader ADD MEMBER [$(ErpLinkAccount)];
GO
