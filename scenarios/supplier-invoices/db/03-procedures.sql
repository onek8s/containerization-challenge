-- Stored procedures of Supplier Invoices.
--   sqlcmd -S sql01.corp.local -E -d SupplierInvoices -i 03-procedures.sql
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE inv.GetActiveChannels
AS
SET NOCOUNT ON;
SELECT c.ChannelId, c.SupplierId, s.Code AS SupplierCode, c.ChannelType, c.Location, c.FilePattern, s.CodePage
FROM inv.Channel c
JOIN inv.Supplier s ON s.SupplierId = c.SupplierId
WHERE c.Active = 1 AND s.Active = 1
ORDER BY s.Code, c.ChannelId;
GO

CREATE OR ALTER PROCEDURE inv.SetChannelPolled
    @ChannelId int,
    @Error nvarchar(1000) = NULL
AS
SET NOCOUNT ON;
UPDATE inv.Channel
SET LastPolledAt = SYSDATETIME(),
    LastError = @Error,
    LastErrorAt = CASE WHEN @Error IS NULL THEN LastErrorAt ELSE SYSDATETIME() END
WHERE ChannelId = @ChannelId;
GO

CREATE OR ALTER PROCEDURE inv.RegisterInboundFile
    @ChannelId int,
    @FileName nvarchar(260),
    @Sha256 binary(32),
    @SizeBytes bigint,
    @FileId bigint OUTPUT
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @SupplierId int = (SELECT SupplierId FROM inv.Channel WHERE ChannelId = @ChannelId);
IF @SupplierId IS NULL THROW 50001, 'Unknown channel.', 1;

BEGIN TRANSACTION;
IF EXISTS (SELECT 1 FROM inv.InboundFile WITH (UPDLOCK, HOLDLOCK) WHERE SupplierId = @SupplierId AND Sha256 = @Sha256)
BEGIN
    SET @FileId = NULL;
END
ELSE
BEGIN
    INSERT inv.InboundFile (ChannelId, SupplierId, FileName, Sha256, SizeBytes, Status)
    VALUES (@ChannelId, @SupplierId, @FileName, @Sha256, @SizeBytes, 'Queued');
    SET @FileId = SCOPE_IDENTITY();
END
COMMIT;
GO

CREATE OR ALTER PROCEDURE inv.SetFileArchived
    @FileId bigint,
    @ArchivePath nvarchar(400)
AS
SET NOCOUNT ON;
UPDATE inv.InboundFile SET ArchivePath = @ArchivePath WHERE FileId = @FileId;
GO

CREATE OR ALTER PROCEDURE inv.SetFileStatus
    @FileId bigint,
    @Status varchar(20),
    @Error nvarchar(2000) = NULL
AS
SET NOCOUNT ON;
UPDATE inv.InboundFile
SET Status = @Status,
    LastError = CASE WHEN @Status = 'Queued' THEN NULL ELSE COALESCE(@Error, LastError) END,
    Attempts = CASE WHEN @Status = 'Queued' THEN 0 ELSE Attempts END,
    ProcessedAt = CASE WHEN @Status IN ('Processed', 'Failed', 'Poison') THEN SYSDATETIME() ELSE NULL END
WHERE FileId = @FileId;
GO

CREATE OR ALTER PROCEDURE inv.AddFileAttempt
    @FileId bigint,
    @Error nvarchar(2000),
    @Attempts int OUTPUT
AS
SET NOCOUNT ON;
UPDATE inv.InboundFile
SET @Attempts = Attempts = Attempts + 1,
    LastError = @Error
WHERE FileId = @FileId;
SET @Attempts = COALESCE(@Attempts, 0);
GO

CREATE OR ALTER PROCEDURE inv.GetInboundFile
    @FileId bigint
AS
SET NOCOUNT ON;
SELECT FileId, ChannelId, SupplierId, FileName, ArchivePath, Status, Attempts
FROM inv.InboundFile
WHERE FileId = @FileId;
GO

CREATE OR ALTER PROCEDURE inv.GetSupplier
    @SupplierId int
AS
SET NOCOUNT ON;
SELECT SupplierId, Code, Name, VatNumber, VendorNumber, CodePage, PriceTolerancePercent, TwoWayMatch, CostCenter
FROM inv.Supplier
WHERE SupplierId = @SupplierId;
GO

CREATE OR ALTER PROCEDURE inv.AddInvoiceHistory
    @InvoiceId bigint,
    @User nvarchar(128),
    @Action varchar(30),
    @Comment nvarchar(1000) = NULL
AS
SET NOCOUNT ON;
INSERT inv.InvoiceHistory (InvoiceId, UserName, Action, Comment)
VALUES (@InvoiceId, @User, @Action, @Comment);
GO

-- Saves a parsed invoice. The same supplier invoice number in another file is a duplicate;
-- the same file processed again replaces the invoice while it is still in accounts payable.
CREATE OR ALTER PROCEDURE inv.SaveInvoice
    @FileId bigint,
    @SupplierId int,
    @VendorNumber varchar(10),
    @InvoiceNumber nvarchar(40),
    @DocumentType smallint,
    @InvoiceDate date,
    @DueDate date,
    @Currency char(3),
    @NetAmount decimal(19,2),
    @VatAmount decimal(19,2),
    @GrossAmount decimal(19,2),
    @PurchaseOrderNumber varchar(20),
    @PaymentReference varchar(35),
    @SupplierVatNumber varchar(20),
    @Status tinyint,
    @MatchSummary nvarchar(400),
    @Lines inv.InvoiceLineType READONLY,
    @Issues inv.IssueType READONLY,
    @InvoiceId bigint OUTPUT,
    @Outcome varchar(10) OUTPUT,
    @CurrentStatus tinyint OUTPUT
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ExistingFile bigint, @ExistingStatus tinyint;

BEGIN TRANSACTION;

SELECT @InvoiceId = InvoiceId, @ExistingFile = FileId, @ExistingStatus = Status
FROM inv.Invoice WITH (UPDLOCK, HOLDLOCK)
WHERE SupplierId = @SupplierId AND InvoiceNumber = @InvoiceNumber AND DocumentType = @DocumentType;

IF @InvoiceId IS NULL
BEGIN
    INSERT inv.Invoice (FileId, SupplierId, VendorNumber, SupplierVatNumber, InvoiceNumber, DocumentType, InvoiceDate, DueDate, Currency,
                        NetAmount, VatAmount, GrossAmount, PurchaseOrderNumber, PaymentReference, Status, MatchSummary)
    VALUES (@FileId, @SupplierId, @VendorNumber, @SupplierVatNumber, @InvoiceNumber, @DocumentType, @InvoiceDate, @DueDate, @Currency,
            @NetAmount, @VatAmount, @GrossAmount, @PurchaseOrderNumber, @PaymentReference, @Status, @MatchSummary);
    SET @InvoiceId = SCOPE_IDENTITY();
    SET @Outcome = 'Inserted';
    SET @CurrentStatus = @Status;
END
ELSE IF @ExistingFile <> @FileId
BEGIN
    SET @Outcome = 'Duplicate';
    SET @CurrentStatus = @ExistingStatus;
    INSERT inv.InvoiceHistory (InvoiceId, UserName, Action, Comment)
    VALUES (@InvoiceId, N'InvoiceProcessor', 'DuplicateReceived', CONCAT(N'Received again in file ', @FileId, N'.'));
    COMMIT;
    RETURN;
END
ELSE IF @ExistingStatus IN (0, 1, 2)
BEGIN
    UPDATE inv.Invoice
    SET VendorNumber = @VendorNumber, SupplierVatNumber = @SupplierVatNumber, InvoiceDate = @InvoiceDate, DueDate = @DueDate,
        Currency = @Currency, NetAmount = @NetAmount, VatAmount = @VatAmount, GrossAmount = @GrossAmount,
        PurchaseOrderNumber = @PurchaseOrderNumber, PaymentReference = @PaymentReference, Status = @Status, MatchSummary = @MatchSummary
    WHERE InvoiceId = @InvoiceId;
    DELETE inv.InvoiceLine WHERE InvoiceId = @InvoiceId;
    DELETE inv.InvoiceIssue WHERE InvoiceId = @InvoiceId AND Resolved = 0;
    SET @Outcome = 'Replaced';
    SET @CurrentStatus = @Status;
END
ELSE
BEGIN
    SET @Outcome = 'Exists';
    SET @CurrentStatus = @ExistingStatus;
    COMMIT;
    RETURN;
END

INSERT inv.InvoiceLine (InvoiceId, LineNumber, ItemNumber, SupplierItemNumber, Description, Quantity, Unit, UnitPrice, NetAmount, VatRate,
                        PurchaseOrderNumber, PurchaseOrderLine, Account)
SELECT @InvoiceId, LineNumber, ItemNumber, SupplierItemNumber, Description, Quantity, Unit, UnitPrice, NetAmount, VatRate,
       PurchaseOrderNumber, PurchaseOrderLine, Account
FROM @Lines;

INSERT inv.InvoiceIssue (InvoiceId, Code, Message)
SELECT @InvoiceId, Code, Message FROM @Issues;

INSERT inv.InvoiceHistory (InvoiceId, UserName, Action, Comment)
VALUES (@InvoiceId, N'InvoiceProcessor', CASE @Outcome WHEN 'Inserted' THEN 'Received' ELSE 'Reprocessed' END, @MatchSummary);

COMMIT;
GO

CREATE OR ALTER PROCEDURE inv.UpdateMatch
    @InvoiceId bigint,
    @Status tinyint,
    @MatchSummary nvarchar(400),
    @User nvarchar(128),
    @Accounts inv.LineAccountType READONLY,
    @Issues inv.IssueType READONLY
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

UPDATE inv.Invoice SET Status = @Status, MatchSummary = @MatchSummary
WHERE InvoiceId = @InvoiceId AND Status IN (1, 2);

IF @@ROWCOUNT = 0
BEGIN
    ROLLBACK;
    THROW 50002, 'The invoice is no longer waiting in accounts payable.', 1;
END

UPDATE l SET Account = a.Account
FROM inv.InvoiceLine l
JOIN @Accounts a ON a.LineNumber = l.LineNumber
WHERE l.InvoiceId = @InvoiceId;

DELETE inv.InvoiceIssue WHERE InvoiceId = @InvoiceId AND Resolved = 0;
INSERT inv.InvoiceIssue (InvoiceId, Code, Message) SELECT @InvoiceId, Code, Message FROM @Issues;

INSERT inv.InvoiceHistory (InvoiceId, UserName, Action, Comment) VALUES (@InvoiceId, @User, 'Rematched', @MatchSummary);
COMMIT;
GO

CREATE OR ALTER PROCEDURE inv.GetInvoice
    @InvoiceId bigint
AS
SET NOCOUNT ON;
SELECT InvoiceId, FileId, SupplierId, VendorNumber, SupplierVatNumber, InvoiceNumber, DocumentType, InvoiceDate, DueDate, Currency,
       NetAmount, VatAmount, GrossAmount, PurchaseOrderNumber, PaymentReference, Status
FROM inv.Invoice
WHERE InvoiceId = @InvoiceId;

SELECT LineNumber, ItemNumber, SupplierItemNumber, Description, Quantity, Unit, UnitPrice, NetAmount, VatRate,
       PurchaseOrderNumber, PurchaseOrderLine, Account
FROM inv.InvoiceLine
WHERE InvoiceId = @InvoiceId
ORDER BY LineNumber;
GO

-- Quantities on invoices that passed matching but are not posted in the ERP yet.
CREATE OR ALTER PROCEDURE inv.GetPendingOrderQuantities
    @PurchaseOrderNumber varchar(20),
    @SupplierId int,
    @InvoiceNumber nvarchar(40)
AS
SET NOCOUNT ON;
SELECT l.PurchaseOrderLine, SUM(l.Quantity) AS Quantity
FROM inv.InvoiceLine l
JOIN inv.Invoice i ON i.InvoiceId = l.InvoiceId
WHERE l.PurchaseOrderNumber = @PurchaseOrderNumber
  AND l.PurchaseOrderLine IS NOT NULL
  AND i.Status IN (1, 3, 4)
  AND NOT (i.SupplierId = @SupplierId AND i.InvoiceNumber = @InvoiceNumber)
GROUP BY l.PurchaseOrderLine;
GO

CREATE OR ALTER PROCEDURE inv.ReleaseInvoice
    @InvoiceId bigint,
    @User nvarchar(128),
    @Comment nvarchar(1000) = NULL,
    @Released bit OUTPUT
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Previous tinyint = (SELECT Status FROM inv.Invoice WITH (UPDLOCK) WHERE InvoiceId = @InvoiceId);

UPDATE inv.Invoice
SET Status = 3, ReleasedBy = @User, ReleasedAt = SYSDATETIME(), BatchId = NULL, ErpMessage = NULL
WHERE InvoiceId = @InvoiceId AND Status IN (1, 2, 6);
SET @Released = CASE WHEN @@ROWCOUNT = 1 THEN 1 ELSE 0 END;

IF @Released = 1
BEGIN
    -- Released again after the ERP rejected it: the next batch takes the new posting.
    IF @Previous = 6 DELETE inv.PostingQueue WHERE InvoiceId = @InvoiceId;
    UPDATE inv.InvoiceIssue SET Resolved = 1 WHERE InvoiceId = @InvoiceId AND Resolved = 0;
    INSERT inv.InvoiceHistory (InvoiceId, UserName, Action, Comment) VALUES (@InvoiceId, @User, 'Released', @Comment);
END
COMMIT;
GO

CREATE OR ALTER PROCEDURE inv.RejectInvoice
    @InvoiceId bigint,
    @User nvarchar(128),
    @Reason nvarchar(1000)
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
UPDATE inv.Invoice SET Status = 7 WHERE InvoiceId = @InvoiceId AND Status IN (1, 2, 6);
IF @@ROWCOUNT = 0
BEGIN
    ROLLBACK;
    THROW 50003, 'The invoice cannot be rejected in its current status.', 1;
END
INSERT inv.InvoiceHistory (InvoiceId, UserName, Action, Comment) VALUES (@InvoiceId, @User, 'Rejected', @Reason);
COMMIT;
GO

CREATE OR ALTER PROCEDURE inv.QueuePosting
    @InvoiceId bigint,
    @Payload nvarchar(max)
AS
SET NOCOUNT ON;
IF NOT EXISTS (SELECT 1 FROM inv.PostingQueue WITH (UPDLOCK, HOLDLOCK) WHERE InvoiceId = @InvoiceId)
    INSERT inv.PostingQueue (InvoiceId, Payload) VALUES (@InvoiceId, @Payload);
GO

CREATE OR ALTER PROCEDURE inv.GetPostingBatch
    @BatchId bigint
AS
SET NOCOUNT ON;
SELECT BatchId, CreatedAt FROM inv.PostingBatch WHERE BatchId = @BatchId;
SELECT Payload FROM inv.PostingQueue WHERE BatchId = @BatchId ORDER BY InvoiceId;
GO

CREATE OR ALTER PROCEDURE inv.CreatePostingBatch
    @MaxInvoices int
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @BatchId bigint;

BEGIN TRANSACTION;
IF NOT EXISTS (SELECT 1 FROM inv.PostingQueue WITH (UPDLOCK, READPAST) WHERE BatchId IS NULL)
BEGIN
    COMMIT;
    RETURN;
END

INSERT inv.PostingBatch (Status) VALUES ('Created');
SET @BatchId = SCOPE_IDENTITY();

WITH pending AS
(
    SELECT TOP (@MaxInvoices) InvoiceId, BatchId
    FROM inv.PostingQueue WITH (UPDLOCK, READPAST)
    WHERE BatchId IS NULL
    ORDER BY QueuedAt, InvoiceId
)
UPDATE pending SET BatchId = @BatchId;

UPDATE i SET Status = 4, BatchId = @BatchId
FROM inv.Invoice i
JOIN inv.PostingQueue q ON q.InvoiceId = i.InvoiceId
WHERE q.BatchId = @BatchId;

UPDATE inv.PostingBatch SET InvoiceCount = (SELECT COUNT(*) FROM inv.PostingQueue WHERE BatchId = @BatchId) WHERE BatchId = @BatchId;
COMMIT;

EXEC inv.GetPostingBatch @BatchId;
GO

CREATE OR ALTER PROCEDURE inv.SetBatchWritten
    @BatchId bigint,
    @FileName nvarchar(260),
    @GrossTotal decimal(19,2)
AS
SET NOCOUNT ON;
UPDATE inv.PostingBatch
SET Status = 'Written', FileName = @FileName, GrossTotal = @GrossTotal, WrittenAt = SYSDATETIME()
WHERE BatchId = @BatchId;

INSERT inv.InvoiceHistory (InvoiceId, UserName, Action, Comment)
SELECT InvoiceId, N'InvoiceErpLink', 'Exported', @FileName FROM inv.PostingQueue WHERE BatchId = @BatchId;
GO

CREATE OR ALTER PROCEDURE inv.ApplyAcknowledgement
    @AckFile nvarchar(260),
    @InvoiceId bigint,
    @Accepted bit,
    @VoucherNumber varchar(30),
    @Message nvarchar(400)
AS
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @BatchId bigint;

BEGIN TRANSACTION;
UPDATE inv.Invoice
SET Status = CASE WHEN @Accepted = 1 THEN 5 ELSE 6 END,
    VoucherNumber = @VoucherNumber,
    PostedAt = CASE WHEN @Accepted = 1 THEN SYSDATETIME() ELSE NULL END,
    ErpMessage = @Message,
    @BatchId = BatchId
WHERE InvoiceId = @InvoiceId AND Status = 4;

IF @@ROWCOUNT = 1
BEGIN
    INSERT inv.InvoiceHistory (InvoiceId, UserName, Action, Comment)
    VALUES (@InvoiceId, N'ERP', CASE WHEN @Accepted = 1 THEN 'Posted' ELSE 'PostingRejected' END,
            CONCAT(@VoucherNumber, CASE WHEN @Message IS NULL THEN N'' ELSE N' ' + @Message END, N' (', @AckFile, N')'));

    IF NOT EXISTS (SELECT 1 FROM inv.Invoice WHERE BatchId = @BatchId AND Status = 4)
        UPDATE inv.PostingBatch SET Status = 'Acknowledged', AckFile = @AckFile, AckAt = SYSDATETIME() WHERE BatchId = @BatchId;
END
COMMIT;
GO
