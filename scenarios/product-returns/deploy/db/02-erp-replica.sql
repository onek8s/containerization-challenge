-- ERP replica: schema erp, loaded every night at 01:00 by the ERP outbound export (SSIS package
-- WWI_ERP_Returns_Replica, owned by the ERP team). Product Returns only reads these tables.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF SCHEMA_ID(N'erp') IS NULL EXEC(N'CREATE SCHEMA erp AUTHORIZATION dbo;');
GO

IF OBJECT_ID(N'erp.SalesOrder', N'U') IS NULL
BEGIN
    CREATE TABLE erp.SalesOrder
    (
        OrderNumber         nvarchar(20)   NOT NULL CONSTRAINT PK_SalesOrder PRIMARY KEY,
        CustomerNumber      nvarchar(20)   NULL,
        CustomerName        nvarchar(200)  NULL,
        CustomerEmail       nvarchar(254)  NULL,
        ShipToName          nvarchar(200)  NULL,
        ShipToStreet        nvarchar(200)  NULL,
        ShipToPostalCode    nvarchar(20)   NULL,
        ShipToCity          nvarchar(100)  NULL,
        ShipToCountry       nvarchar(2)    NULL,
        OrderDate           datetime2(7)   NOT NULL,
        DeliveredDate       datetime2(7)   NULL,
        Currency            nvarchar(3)    NULL
    );
END
GO

IF OBJECT_ID(N'erp.SalesOrderLine', N'U') IS NULL
BEGIN
    CREATE TABLE erp.SalesOrderLine
    (
        OrderNumber         nvarchar(20)   NOT NULL CONSTRAINT FK_SalesOrderLine_SalesOrder REFERENCES erp.SalesOrder (OrderNumber) ON DELETE CASCADE,
        LineNumber          int            NOT NULL,
        Sku                 nvarchar(40)   NULL,
        Description         nvarchar(200)  NULL,
        Quantity            int            NOT NULL,
        UnitPrice           decimal(12,2)  NOT NULL,
        CONSTRAINT PK_SalesOrderLine PRIMARY KEY (OrderNumber, LineNumber)
    );
END
GO

IF OBJECT_ID(N'erp.Item', N'U') IS NULL
BEGIN
    CREATE TABLE erp.Item
    (
        Sku                 nvarchar(40)   NOT NULL CONSTRAINT PK_Item PRIMARY KEY,
        Description         nvarchar(200)  NULL,
        Category            nvarchar(40)   NULL,
        FinalSale           bit            NOT NULL CONSTRAINT DF_Item_FinalSale DEFAULT (0),
        Returnable          bit            NOT NULL CONSTRAINT DF_Item_Returnable DEFAULT (1),
        WeightKg            decimal(9,3)   NOT NULL CONSTRAINT DF_Item_WeightKg DEFAULT (0)
    );
END
GO
