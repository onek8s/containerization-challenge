-- Stored procedures used by the API and the sales office site. Re-runnable (CREATE OR ALTER, SQL Server 2016 SP1+).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.usp_GetCatalogChanges
    @SinceVersion bigint
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ItemNo, Name, Category, Unit, CaseSize, ListPrice, VatRate, MinOrderQty, Active,
           CAST(RowVer AS bigint) AS Version
    FROM dbo.Product
    WHERE CAST(RowVer AS bigint) > @SinceVersion
      AND (@SinceVersion > 0 OR Active = 1)
    ORDER BY ItemNo;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_SubmitOrder
    @ClientOrderId uniqueidentifier,
    @RepReference nvarchar(20),
    @CustomerNo nvarchar(20),
    @RepCode nvarchar(4),
    @SubmittedBy nvarchar(256),
    @CreatedAt datetime,
    @RequestedDelivery datetime,
    @Status int,
    @NetTotal decimal(14,2),
    @VatTotal decimal(14,2),
    @ManualDiscountPercent decimal(5,2),
    @Comment nvarchar(500),
    @ClientVersion nvarchar(30),
    @Lines dbo.OrderLineType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    -- Two syncs of the same laptop can race (timer and button); the key range lock makes the second one wait.
    DECLARE @ExistingId int, @ExistingNo nvarchar(20), @ExistingStatus int;
    SELECT @ExistingId = SalesOrderId, @ExistingNo = OrderNo, @ExistingStatus = Status
    FROM dbo.SalesOrder WITH (UPDLOCK, HOLDLOCK)
    WHERE ClientOrderId = @ClientOrderId;

    IF @ExistingId IS NOT NULL
    BEGIN
        COMMIT;
        SELECT @ExistingId AS SalesOrderId, @ExistingNo AS OrderNo, @ExistingStatus AS Status, CAST(1 AS bit) AS AlreadyReceived;
        RETURN;
    END

    DECLARE @CustomerId int = (SELECT CustomerId FROM dbo.Customer WHERE CustomerNo = @CustomerNo);
    IF @CustomerId IS NULL
        THROW 50010, 'Unknown customer.', 1;

    DECLARE @OrderNo nvarchar(20) = N'SO' + CAST(NEXT VALUE FOR dbo.SalesOrderNumber AS nvarchar(10));

    INSERT dbo.SalesOrder (OrderNo, ClientOrderId, RepReference, CustomerId, CustomerNo, RepCode, SubmittedBy, CreatedAt,
                           RequestedDelivery, Status, NetTotal, VatTotal, ManualDiscountPercent, Comment, ClientVersion)
    VALUES (@OrderNo, @ClientOrderId, @RepReference, @CustomerId, @CustomerNo, NULLIF(@RepCode, N''), @SubmittedBy, @CreatedAt,
            @RequestedDelivery, @Status, @NetTotal, @VatTotal, @ManualDiscountPercent, @Comment, @ClientVersion);

    DECLARE @SalesOrderId int = SCOPE_IDENTITY();

    INSERT dbo.SalesOrderLine (SalesOrderId, [LineNo], ItemNo, ItemName, Quantity, UnitPrice, LineDiscountPercent, VatRate, LineTotal)
    SELECT @SalesOrderId, [LineNo], ItemNo, ItemName, Quantity, UnitPrice, LineDiscountPercent, VatRate, LineTotal
    FROM @Lines;

    COMMIT;

    SELECT @SalesOrderId AS SalesOrderId, @OrderNo AS OrderNo, @Status AS Status, CAST(0 AS bit) AS AlreadyReceived;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_DecideOrder
    @SalesOrderId int,
    @Approve bit,
    @DecidedBy nvarchar(256),
    @Comment nvarchar(400)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.SalesOrder
    SET Status = CASE WHEN @Approve = 1 THEN 1 ELSE 3 END,
        DecidedBy = @DecidedBy,
        DecidedAt = GETDATE(),
        DecisionComment = @Comment
    WHERE SalesOrderId = @SalesOrderId AND Status = 2;

    IF @@ROWCOUNT = 0
        THROW 50020, 'The order is not waiting for approval. Another manager may have handled it already.', 1;

    SELECT o.Status, o.OrderNo, c.Email AS CustomerEmail
    FROM dbo.SalesOrder o
    JOIN dbo.Customer c ON c.CustomerId = o.CustomerId
    WHERE o.SalesOrderId = @SalesOrderId;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_ImportPriceList
    @FileName nvarchar(260),
    @ImportedBy nvarchar(256),
    @Products dbo.PriceListRowType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Changes TABLE (Action nvarchar(10));

    BEGIN TRANSACTION;

    -- Only rows that really change are touched so that the clients' incremental sync stays small.
    MERGE dbo.Product WITH (HOLDLOCK) AS t
    USING @Products AS s ON t.ItemNo = s.ItemNo
    WHEN MATCHED AND (t.Name <> s.Name OR t.Category <> s.Category OR t.Unit <> s.Unit OR t.CaseSize <> s.CaseSize
                      OR t.ListPrice <> s.ListPrice OR t.VatRate <> s.VatRate OR t.MinOrderQty <> s.MinOrderQty OR t.Active <> s.Active)
        THEN UPDATE SET Name = s.Name, Category = s.Category, Unit = s.Unit, CaseSize = s.CaseSize, ListPrice = s.ListPrice,
                        VatRate = s.VatRate, MinOrderQty = s.MinOrderQty, Active = s.Active
    WHEN NOT MATCHED BY TARGET
        THEN INSERT (ItemNo, Name, Category, Unit, CaseSize, ListPrice, VatRate, MinOrderQty, Active)
             VALUES (s.ItemNo, s.Name, s.Category, s.Unit, s.CaseSize, s.ListPrice, s.VatRate, s.MinOrderQty, s.Active)
    OUTPUT $action INTO @Changes;

    DECLARE @Inserted int = (SELECT COUNT(*) FROM @Changes WHERE Action = N'INSERT');
    DECLARE @Updated int = (SELECT COUNT(*) FROM @Changes WHERE Action = N'UPDATE');

    UPDATE dbo.CatalogState SET PriceListVersion = PriceListVersion + 1 WHERE Id = 1;
    DECLARE @Version bigint = (SELECT PriceListVersion FROM dbo.CatalogState WHERE Id = 1);

    INSERT dbo.PriceListImport (FileName, ImportedBy, Inserted, Updated, PriceListVersion)
    VALUES (@FileName, @ImportedBy, @Inserted, @Updated, @Version);

    COMMIT;

    SELECT @Inserted AS Inserted, @Updated AS Updated, @Version AS PriceListVersion;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_SavePromotion
    @ItemNo nvarchar(20),
    @PromoPrice decimal(12,2),
    @ValidFrom datetime,
    @ValidTo datetime,
    @CreatedBy nvarchar(256)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    -- One promotion per item and period: an overlapping promotion is replaced.
    DELETE dbo.Promotion WHERE ItemNo = @ItemNo AND ValidFrom <= @ValidTo AND ValidTo >= @ValidFrom;

    INSERT dbo.Promotion (ItemNo, PromoPrice, ValidFrom, ValidTo, CreatedBy)
    VALUES (@ItemNo, @PromoPrice, @ValidFrom, @ValidTo, @CreatedBy);

    UPDATE dbo.CatalogState SET PriceListVersion = PriceListVersion + 1 WHERE Id = 1;
    COMMIT;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_DeletePromotion
    @PromotionId int
AS
BEGIN
    SET NOCOUNT ON;
    DELETE dbo.Promotion WHERE PromotionId = @PromotionId;
    IF @@ROWCOUNT > 0
        UPDATE dbo.CatalogState SET PriceListVersion = PriceListVersion + 1 WHERE Id = 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_MarkExported
    @OrderNo nvarchar(20),
    @ErpOrderNo nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.SalesOrder
    SET Status = 4, ErpOrderNo = @ErpOrderNo, ExportedAt = GETDATE()
    WHERE OrderNo = @OrderNo AND Status = 1 AND ExportedAt IS NULL;

    SELECT @@ROWCOUNT;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_UpsertCustomers
    @Customers dbo.CustomerRowType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Changes TABLE (Action nvarchar(10));

    -- E-mail, rep assignment and delivery days are maintained by the sales office and are not overwritten.
    MERGE dbo.Customer WITH (HOLDLOCK) AS t
    USING @Customers AS s ON t.CustomerNo = s.CustomerNo
    WHEN MATCHED AND (t.Name <> s.Name OR ISNULL(t.Street, N'') <> ISNULL(s.Street, N'') OR ISNULL(t.PostalCode, N'') <> ISNULL(s.PostalCode, N'')
                      OR ISNULL(t.City, N'') <> ISNULL(s.City, N'') OR t.PriceGroup <> s.PriceGroup OR t.CreditBlocked <> s.CreditBlocked)
        THEN UPDATE SET Name = s.Name, Street = s.Street, PostalCode = s.PostalCode, City = s.City, PriceGroup = s.PriceGroup,
                        CreditBlocked = s.CreditBlocked, ModifiedAt = GETDATE()
    WHEN NOT MATCHED BY TARGET
        THEN INSERT (CustomerNo, Name, Street, PostalCode, City, PriceGroup, CreditBlocked)
             VALUES (s.CustomerNo, s.Name, s.Street, s.PostalCode, s.City, s.PriceGroup, s.CreditBlocked)
    OUTPUT $action INTO @Changes;

    SELECT (SELECT COUNT(*) FROM @Changes WHERE Action = N'INSERT') AS Inserted,
           (SELECT COUNT(*) FROM @Changes WHERE Action = N'UPDATE') AS Updated,
           CAST(0 AS bigint) AS PriceListVersion;
END
GO

-- Removes refresh tokens that expired more than a week ago. Scheduled as a SQL Server Agent job step.
CREATE OR ALTER PROCEDURE dbo.usp_PurgeRefreshTokens
AS
BEGIN
    SET NOCOUNT ON;
    DELETE dbo.RefreshToken WHERE ExpiresUtc < DATEADD(day, -7, GETUTCDATE());
END
GO
