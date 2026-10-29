-- Product Returns database: application schema rtn.
-- Run with sqlcmd against sql01 (see Initialize-Database.ps1).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF SCHEMA_ID(N'rtn') IS NULL EXEC(N'CREATE SCHEMA rtn AUTHORIZATION dbo;');
GO

IF OBJECT_ID(N'rtn.ReturnRequest', N'U') IS NULL
BEGIN
    CREATE TABLE rtn.ReturnRequest
    (
        Id                  int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReturnRequest PRIMARY KEY,
        RmaNumber           nvarchar(20)   NULL,
        OrderNumber         nvarchar(20)   NOT NULL,
        CustomerName        nvarchar(200)  NULL,
        CustomerEmail       nvarchar(254)  NULL,
        Currency            nvarchar(3)    NOT NULL CONSTRAINT DF_ReturnRequest_Currency DEFAULT (N'EUR'),
        Status              int            NOT NULL,
        CreatedUtc          datetime2(7)   NOT NULL,
        CreatedBy           nvarchar(100)  NULL,
        DecidedUtc          datetime2(7)   NULL,
        DecidedBy           nvarchar(100)  NULL,
        RejectReason        nvarchar(500)  NULL,
        TrackingNumber      nvarchar(40)   NULL,
        LabelPath           nvarchar(400)  NULL,
        LabelCreatedUtc     datetime2(7)   NULL,
        LabelAttempts       int            NOT NULL CONSTRAINT DF_ReturnRequest_LabelAttempts DEFAULT (0),
        LabelError          nvarchar(1000) NULL,
        ReminderSentUtc     datetime2(7)   NULL,
        ReceivedUtc         datetime2(7)   NULL,
        InspectedUtc        datetime2(7)   NULL,
        InspectedBy         nvarchar(100)  NULL,
        RefundAmount        decimal(12,2)  NULL,
        RestockingFee       decimal(12,2)  NULL,
        LabelFee            decimal(12,2)  NULL,
        RefundExportedUtc   datetime2(7)   NULL,
        RefundBatch         nvarchar(20)   NULL,
        AnonymizedUtc       datetime2(7)   NULL,
        RowVersion          rowversion     NOT NULL
    );

    CREATE UNIQUE INDEX IX_ReturnRequest_RmaNumber ON rtn.ReturnRequest (RmaNumber) WHERE RmaNumber IS NOT NULL;
    CREATE INDEX IX_ReturnRequest_Status_CreatedUtc ON rtn.ReturnRequest (Status, CreatedUtc);
    CREATE INDEX IX_ReturnRequest_OrderNumber ON rtn.ReturnRequest (OrderNumber);
END
GO

IF OBJECT_ID(N'rtn.ReturnLine', N'U') IS NULL
BEGIN
    CREATE TABLE rtn.ReturnLine
    (
        Id                  int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReturnLine PRIMARY KEY,
        ReturnRequestId     int            NOT NULL CONSTRAINT FK_ReturnLine_ReturnRequest REFERENCES rtn.ReturnRequest (Id) ON DELETE CASCADE,
        OrderLineNumber     int            NOT NULL,
        Sku                 nvarchar(40)   NOT NULL,
        Description         nvarchar(200)  NULL,
        Category            nvarchar(40)   NULL,
        Quantity            int            NOT NULL,
        UnitPrice           decimal(12,2)  NOT NULL,
        ReasonCode          nvarchar(30)   NOT NULL,
        CustomerComment     nvarchar(1000) NULL,
        ReceivedQuantity    int            NOT NULL CONSTRAINT DF_ReturnLine_ReceivedQuantity DEFAULT (0),
        Condition           int            NOT NULL CONSTRAINT DF_ReturnLine_Condition DEFAULT (0),
        RefundAmount        decimal(12,2)  NULL
    );

    CREATE INDEX IX_ReturnLine_ReturnRequestId ON rtn.ReturnLine (ReturnRequestId);
END
GO

IF OBJECT_ID(N'rtn.ReturnNote', N'U') IS NULL
BEGIN
    CREATE TABLE rtn.ReturnNote
    (
        Id                  int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReturnNote PRIMARY KEY,
        ReturnRequestId     int            NOT NULL CONSTRAINT FK_ReturnNote_ReturnRequest REFERENCES rtn.ReturnRequest (Id) ON DELETE CASCADE,
        Author              nvarchar(100)  NOT NULL,
        Text                nvarchar(2000) NOT NULL,
        CreatedUtc          datetime2(7)   NOT NULL
    );

    CREATE INDEX IX_ReturnNote_ReturnRequestId ON rtn.ReturnNote (ReturnRequestId);
END
GO

IF OBJECT_ID(N'rtn.ScannerDevice', N'U') IS NULL
BEGIN
    CREATE TABLE rtn.ScannerDevice
    (
        Id                  int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ScannerDevice PRIMARY KEY,
        Name                nvarchar(50)   NOT NULL CONSTRAINT UQ_ScannerDevice_Name UNIQUE,
        Location            nvarchar(100)  NULL,
        ApiKeyHash          nvarchar(64)   NOT NULL,
        Enabled             bit            NOT NULL CONSTRAINT DF_ScannerDevice_Enabled DEFAULT (1),
        LastSeenUtc         datetime2(7)   NULL
    );
END
GO
