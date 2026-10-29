-- Tables and table types of Supplier Invoices.
--   sqlcmd -S sql01.corp.local -E -d SupplierInvoices -i 02-schema.sql
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF SCHEMA_ID(N'inv') IS NULL EXEC (N'CREATE SCHEMA inv AUTHORIZATION dbo');
GO

CREATE TABLE inv.Supplier
(
    SupplierId            int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Supplier PRIMARY KEY,
    Code                  varchar(10)   NOT NULL CONSTRAINT UQ_Supplier_Code UNIQUE,
    Name                  nvarchar(100) NOT NULL,
    VatNumber             varchar(20)   NOT NULL,
    VendorNumber          varchar(10)   NULL,
    CodePage              int           NOT NULL CONSTRAINT DF_Supplier_CodePage DEFAULT (1252),
    PriceTolerancePercent decimal(5,2)  NULL,
    TwoWayMatch           bit           NOT NULL CONSTRAINT DF_Supplier_TwoWay DEFAULT (0),
    CostCenter            varchar(6)    NULL,
    ContactEmail          nvarchar(200) NULL,
    Active                bit           NOT NULL CONSTRAINT DF_Supplier_Active DEFAULT (1),
    ModifiedBy            nvarchar(128) NULL,
    ModifiedAt            datetime2(0)  NOT NULL CONSTRAINT DF_Supplier_ModifiedAt DEFAULT (SYSDATETIME())
);
GO

CREATE TABLE inv.Channel
(
    ChannelId    int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Channel PRIMARY KEY,
    SupplierId   int            NOT NULL CONSTRAINT FK_Channel_Supplier REFERENCES inv.Supplier (SupplierId),
    ChannelType  varchar(5)     NOT NULL CONSTRAINT CK_Channel_Type CHECK (ChannelType IN ('FTP', 'SHARE')),
    Location     nvarchar(260)  NOT NULL,
    FilePattern  nvarchar(100)  NOT NULL CONSTRAINT DF_Channel_Pattern DEFAULT (N'*.*'),
    Active       bit            NOT NULL CONSTRAINT DF_Channel_Active DEFAULT (1),
    LastPolledAt datetime2(0)   NULL,
    LastError    nvarchar(1000) NULL,
    LastErrorAt  datetime2(0)   NULL
);
GO

CREATE TABLE inv.InboundFile
(
    FileId      bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_InboundFile PRIMARY KEY,
    ChannelId   int            NOT NULL CONSTRAINT FK_InboundFile_Channel REFERENCES inv.Channel (ChannelId),
    SupplierId  int            NOT NULL CONSTRAINT FK_InboundFile_Supplier REFERENCES inv.Supplier (SupplierId),
    FileName    nvarchar(260)  NOT NULL,
    Sha256      binary(32)     NOT NULL,
    SizeBytes   bigint         NOT NULL,
    ArchivePath nvarchar(400)  NULL,
    ReceivedAt  datetime2(0)   NOT NULL CONSTRAINT DF_InboundFile_ReceivedAt DEFAULT (SYSDATETIME()),
    Status      varchar(20)    NOT NULL CONSTRAINT CK_InboundFile_Status CHECK (Status IN ('Queued', 'Processed', 'Failed', 'Duplicate', 'Poison')),
    Attempts    int            NOT NULL CONSTRAINT DF_InboundFile_Attempts DEFAULT (0),
    LastError   nvarchar(2000) NULL,
    ProcessedAt datetime2(0)   NULL
);
GO
CREATE UNIQUE INDEX UX_InboundFile_Content ON inv.InboundFile (SupplierId, Sha256);
CREATE INDEX IX_InboundFile_Status ON inv.InboundFile (Status, ReceivedAt) WHERE Status IN ('Queued', 'Failed', 'Poison');
GO

-- Status: 0 Received, 1 Matched, 2 Exception, 3 Released, 4 Posting, 5 Posted, 6 PostingFailed, 7 Rejected
CREATE TABLE inv.Invoice
(
    InvoiceId           bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Invoice PRIMARY KEY,
    FileId              bigint         NOT NULL CONSTRAINT FK_Invoice_File REFERENCES inv.InboundFile (FileId),
    SupplierId          int            NOT NULL CONSTRAINT FK_Invoice_Supplier REFERENCES inv.Supplier (SupplierId),
    VendorNumber        varchar(10)    NULL,
    SupplierVatNumber   varchar(20)    NULL,
    InvoiceNumber       nvarchar(40)   NOT NULL,
    DocumentType        smallint       NOT NULL,
    InvoiceDate         date           NOT NULL,
    DueDate             date           NULL,
    Currency            char(3)        NOT NULL,
    NetAmount           decimal(19,2)  NOT NULL,
    VatAmount           decimal(19,2)  NOT NULL,
    GrossAmount         decimal(19,2)  NOT NULL,
    PurchaseOrderNumber varchar(20)    NULL,
    PaymentReference    varchar(35)    NULL,
    Status              tinyint        NOT NULL,
    MatchSummary        nvarchar(400)  NULL,
    ReceivedAt          datetime2(0)   NOT NULL CONSTRAINT DF_Invoice_ReceivedAt DEFAULT (SYSDATETIME()),
    ReleasedBy          nvarchar(128)  NULL,
    ReleasedAt          datetime2(0)   NULL,
    BatchId             bigint         NULL,
    VoucherNumber       varchar(30)    NULL,
    PostedAt            datetime2(0)   NULL,
    ErpMessage          nvarchar(400)  NULL,
    RowVersion          rowversion     NOT NULL,
    CONSTRAINT UQ_Invoice_Number UNIQUE (SupplierId, InvoiceNumber, DocumentType)
);
GO
CREATE INDEX IX_Invoice_Status ON inv.Invoice (Status, ReceivedAt) INCLUDE (SupplierId, InvoiceNumber, GrossAmount);
CREATE INDEX IX_Invoice_PurchaseOrder ON inv.Invoice (PurchaseOrderNumber) WHERE PurchaseOrderNumber IS NOT NULL;
CREATE INDEX IX_Invoice_File ON inv.Invoice (FileId);
GO

CREATE TABLE inv.InvoiceLine
(
    InvoiceId           bigint         NOT NULL CONSTRAINT FK_InvoiceLine_Invoice REFERENCES inv.Invoice (InvoiceId),
    LineNumber          int            NOT NULL,
    ItemNumber          varchar(30)    NULL,
    SupplierItemNumber  varchar(35)    NULL,
    Description         nvarchar(200)  NULL,
    Quantity            decimal(18,3)  NOT NULL,
    Unit                varchar(5)     NULL,
    UnitPrice           decimal(19,4)  NOT NULL,
    NetAmount           decimal(19,2)  NOT NULL,
    VatRate             decimal(5,2)   NOT NULL,
    PurchaseOrderNumber varchar(20)    NULL,
    PurchaseOrderLine   int            NULL,
    Account             varchar(8)     NULL,
    CONSTRAINT PK_InvoiceLine PRIMARY KEY (InvoiceId, LineNumber)
);
GO
CREATE INDEX IX_InvoiceLine_Order ON inv.InvoiceLine (PurchaseOrderNumber, PurchaseOrderLine) INCLUDE (Quantity) WHERE PurchaseOrderNumber IS NOT NULL;
GO

CREATE TABLE inv.InvoiceIssue
(
    IssueId   bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_InvoiceIssue PRIMARY KEY,
    InvoiceId bigint        NOT NULL CONSTRAINT FK_InvoiceIssue_Invoice REFERENCES inv.Invoice (InvoiceId),
    Code      varchar(30)   NOT NULL,
    Message   nvarchar(400) NOT NULL,
    Resolved  bit           NOT NULL CONSTRAINT DF_InvoiceIssue_Resolved DEFAULT (0)
);
GO
CREATE INDEX IX_InvoiceIssue_Invoice ON inv.InvoiceIssue (InvoiceId);
GO

CREATE TABLE inv.InvoiceHistory
(
    HistoryId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_InvoiceHistory PRIMARY KEY,
    InvoiceId bigint         NOT NULL CONSTRAINT FK_InvoiceHistory_Invoice REFERENCES inv.Invoice (InvoiceId),
    At        datetime2(0)   NOT NULL CONSTRAINT DF_InvoiceHistory_At DEFAULT (SYSDATETIME()),
    UserName  nvarchar(128)  NOT NULL,
    Action    varchar(30)    NOT NULL,
    Comment   nvarchar(1000) NULL
);
GO
CREATE INDEX IX_InvoiceHistory_Invoice ON inv.InvoiceHistory (InvoiceId, At);
GO

CREATE TABLE inv.PostingBatch
(
    BatchId      bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_PostingBatch PRIMARY KEY,
    CreatedAt    datetime2(0)  NOT NULL CONSTRAINT DF_PostingBatch_CreatedAt DEFAULT (SYSDATETIME()),
    Status       varchar(12)   NOT NULL CONSTRAINT CK_PostingBatch_Status CHECK (Status IN ('Created', 'Written', 'Acknowledged')),
    FileName     nvarchar(260) NULL,
    InvoiceCount int           NOT NULL CONSTRAINT DF_PostingBatch_Count DEFAULT (0),
    GrossTotal   decimal(19,2) NULL,
    WrittenAt    datetime2(0)  NULL,
    AckFile      nvarchar(260) NULL,
    AckAt        datetime2(0)  NULL
);
GO

CREATE TABLE inv.PostingQueue
(
    InvoiceId bigint        NOT NULL CONSTRAINT PK_PostingQueue PRIMARY KEY CONSTRAINT FK_PostingQueue_Invoice REFERENCES inv.Invoice (InvoiceId),
    Payload   nvarchar(max) NOT NULL,
    QueuedAt  datetime2(0)  NOT NULL CONSTRAINT DF_PostingQueue_QueuedAt DEFAULT (SYSDATETIME()),
    BatchId   bigint        NULL CONSTRAINT FK_PostingQueue_Batch REFERENCES inv.PostingBatch (BatchId)
);
GO
CREATE INDEX IX_PostingQueue_Pending ON inv.PostingQueue (QueuedAt) WHERE BatchId IS NULL;
GO

ALTER TABLE inv.Invoice ADD CONSTRAINT FK_Invoice_Batch FOREIGN KEY (BatchId) REFERENCES inv.PostingBatch (BatchId);
GO

CREATE TYPE inv.InvoiceLineType AS TABLE
(
    LineNumber          int           NOT NULL PRIMARY KEY,
    ItemNumber          varchar(30)   NULL,
    SupplierItemNumber  varchar(35)   NULL,
    Description         nvarchar(200) NULL,
    Quantity            decimal(18,3) NOT NULL,
    Unit                varchar(5)    NULL,
    UnitPrice           decimal(19,4) NOT NULL,
    NetAmount           decimal(19,2) NOT NULL,
    VatRate             decimal(5,2)  NOT NULL,
    PurchaseOrderNumber varchar(20)   NULL,
    PurchaseOrderLine   int           NULL,
    Account             varchar(8)    NULL
);
GO

CREATE TYPE inv.IssueType AS TABLE
(
    Code    varchar(30)   NOT NULL,
    Message nvarchar(400) NOT NULL
);
GO

CREATE TYPE inv.LineAccountType AS TABLE
(
    LineNumber int        NOT NULL PRIMARY KEY,
    Account    varchar(8) NULL
);
GO

CREATE VIEW inv.InvoiceOverview
AS
SELECT i.InvoiceId, i.FileId, i.SupplierId, s.Code AS SupplierCode, s.Name AS SupplierName, i.VendorNumber,
       i.InvoiceNumber, i.DocumentType, i.InvoiceDate, i.DueDate, i.Currency, i.NetAmount, i.VatAmount, i.GrossAmount,
       i.PurchaseOrderNumber, i.PaymentReference, i.Status, i.MatchSummary, i.ReceivedAt, i.ReleasedBy, i.ReleasedAt,
       i.BatchId, i.VoucherNumber, i.PostedAt, i.ErpMessage,
       (SELECT COUNT(*) FROM inv.InvoiceIssue x WHERE x.InvoiceId = i.InvoiceId AND x.Resolved = 0) AS OpenIssues
FROM inv.Invoice i
JOIN inv.Supplier s ON s.SupplierId = i.SupplierId;
GO
