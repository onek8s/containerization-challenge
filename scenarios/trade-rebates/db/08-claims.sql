:on error exit
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
USE [$(DatabaseName)];
GO

CREATE OR ALTER PROCEDURE rebate.WriteAudit
    @Actor nvarchar(128), @Action varchar(40), @Entity varchar(40), @EntityId nvarchar(40), @Detail nvarchar(2000) = NULL
AS
SET NOCOUNT ON;
INSERT rebate.AuditLog (Actor, Action, Entity, EntityId, Detail)
VALUES (ISNULL(@Actor, ORIGINAL_LOGIN()), @Action, @Entity, @EntityId, @Detail);
GO

CREATE OR ALTER PROCEDURE rebate.RaiseEvent @EventId int, @Level nchar(1), @Message nvarchar(4000)
AS
SET NOCOUNT ON;
IF rebate.GetSetting('EventLogEnabled') = N'1'
BEGIN
    BEGIN TRY
        EXEC rebate.clr_WriteEvent @EventId, @Level, @Message;
    END TRY
    BEGIN CATCH
        -- The event log is best effort; the audit log always has the details.
    END CATCH
END
GO

----------------------------------------------------------------------------------------------
-- Price book lookup (Access price book through the linked server)
----------------------------------------------------------------------------------------------
-- Fills #price (Gtin char(14), NetPrice decimal(19,4)) created by the caller.
CREATE OR ALTER PROCEDURE rebate.LoadPriceBook @DistributorCode nvarchar(20), @From date, @To date
AS
SET NOCOUNT ON;
DECLARE @server sysname = rebate.GetSetting('PriceBookServer');
IF @server IS NULL OR NOT EXISTS (SELECT 1 FROM sys.servers WHERE name = @server)
    THROW 50030, N'Price book linked server is not configured.', 1;

-- Access SQL: date literals in #yyyy-mm-dd#.
DECLARE @access nvarchar(2000) =
      N'SELECT n.EAN, n.NetPrice FROM NetPrice AS n INNER JOIN PriceGroup AS g ON g.PriceGroup = n.PriceGroup'
    + N' WHERE g.DistributorCode = ''' + REPLACE(@DistributorCode, N'''', N'''''') + N''''
    + N' AND n.ValidFrom <= #' + CONVERT(nchar(10), @To, 120) + N'#'
    + N' AND (n.ValidTo IS NULL OR n.ValidTo >= #' + CONVERT(nchar(10), @From, 120) + N'#)';

DECLARE @sql nvarchar(max) =
      N'INSERT #price (Gtin, NetPrice) SELECT RIGHT(REPLICATE(''0'', 14) + LTRIM(RTRIM(CAST(EAN AS nvarchar(14)))), 14) AS Gtin, MIN(CAST(NetPrice AS decimal(19,4))) AS NetPrice'
    + N' FROM OPENQUERY(' + QUOTENAME(@server) + N', ''' + REPLACE(@access, N'''', N'''''') + N''')'
    + N' GROUP BY RIGHT(REPLICATE(''0'', 14) + LTRIM(RTRIM(CAST(EAN AS nvarchar(14)))), 14);';
EXEC (@sql);
GO

----------------------------------------------------------------------------------------------
-- Claim validation
----------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE rebate.ValidateClaim @ClaimId int
AS
SET NOCOUNT, XACT_ABORT ON;

DECLARE @DistributorId int, @Period int, @Status char(1), @Code nvarchar(20);
SELECT @DistributorId = c.DistributorId, @Period = c.Period, @Status = c.Status, @Code = d.Code
FROM rebate.Claim AS c
JOIN rebate.Distributor AS d ON d.DistributorId = c.DistributorId
WHERE c.ClaimId = @ClaimId;

IF @DistributorId IS NULL
    THROW 50004, N'Claim not found.', 1;
IF @Status NOT IN ('R', 'V')
    THROW 50005, N'Only received or validated claims can be validated.', 1;

DECLARE @PeriodStart date = DATEFROMPARTS(@Period / 100, @Period % 100, 1);
DECLARE @PeriodEnd date = EOMONTH(@PeriodStart);

UPDATE rebate.ClaimLine SET LineStatus = 'P', Issue = NULL, ListNetPrice = NULL WHERE ClaimId = @ClaimId;

UPDATE rebate.ClaimLine SET LineStatus = 'X', Issue = N'GTIN check digit is not valid.'
WHERE ClaimId = @ClaimId AND rebate.GtinIsValid(Gtin) = 0;

UPDATE l SET LineStatus = 'X', Issue = N'Product is not in the rebate catalogue.'
FROM rebate.ClaimLine AS l
WHERE l.ClaimId = @ClaimId AND l.LineStatus = 'P'
  AND NOT EXISTS (SELECT 1 FROM rebate.Product AS p WHERE p.Gtin = l.Gtin AND p.IsActive = 1);

UPDATE rebate.ClaimLine SET LineStatus = 'X', Issue = N'Invoice date is outside the claim period.'
WHERE ClaimId = @ClaimId AND LineStatus = 'P' AND (InvoiceDate < @PeriodStart OR InvoiceDate > @PeriodEnd);

WITH d AS
(
    SELECT LineNumber, ROW_NUMBER() OVER (PARTITION BY InvoiceNo, Gtin ORDER BY LineNumber) AS rn
    FROM rebate.ClaimLine WHERE ClaimId = @ClaimId AND LineStatus = 'P'
)
UPDATE l SET LineStatus = 'X', Issue = N'Invoice line repeated in this claim.'
FROM rebate.ClaimLine AS l JOIN d ON d.LineNumber = l.LineNumber
WHERE l.ClaimId = @ClaimId AND d.rn > 1;

UPDATE l SET LineStatus = 'X', Issue = N'Already claimed in claim ' + CAST(o.ClaimId AS nvarchar(12)) + N'.'
FROM rebate.ClaimLine AS l
CROSS APPLY (
    SELECT TOP (1) oc.ClaimId
    FROM rebate.ClaimLine AS ol
    JOIN rebate.Claim AS oc ON oc.ClaimId = ol.ClaimId
    WHERE ol.InvoiceNo = l.InvoiceNo AND ol.Gtin = l.Gtin AND ol.ClaimId <> l.ClaimId
      AND oc.DistributorId = @DistributorId AND oc.Status IN ('V', 'A') AND ol.LineStatus IN ('K', 'W')
    ORDER BY oc.ClaimId
) AS o
WHERE l.ClaimId = @ClaimId AND l.LineStatus = 'P';

IF rebate.GetSetting('PriceBookCheck') = N'On'
BEGIN
    CREATE TABLE #price (Gtin char(14) NOT NULL PRIMARY KEY, NetPrice decimal(19,4) NOT NULL);
    DECLARE @tolerance decimal(9,4) = TRY_CONVERT(decimal(9,4), rebate.GetSetting('PriceTolerancePercent'));

    BEGIN TRY
        EXEC rebate.LoadPriceBook @Code, @PeriodStart, @PeriodEnd;

        UPDATE l
        SET ListNetPrice = p.NetPrice,
            LineStatus = CASE WHEN l.Quantity > 0 AND l.NetAmount / l.Quantity < p.NetPrice * (1 - ISNULL(@tolerance, 15) / 100) THEN 'W' ELSE l.LineStatus END,
            Issue = CASE WHEN l.Quantity > 0 AND l.NetAmount / l.Quantity < p.NetPrice * (1 - ISNULL(@tolerance, 15) / 100)
                         THEN N'Unit price ' + FORMAT(l.NetAmount / l.Quantity, N'0.00##') + N' is below price book ' + FORMAT(p.NetPrice, N'0.00##') + N'.'
                         ELSE l.Issue END
        FROM rebate.ClaimLine AS l
        JOIN #price AS p ON p.Gtin = l.Gtin
        WHERE l.ClaimId = @ClaimId AND l.LineStatus = 'P';

        UPDATE l SET LineStatus = 'W', Issue = N'No price book entry for the claim period.'
        FROM rebate.ClaimLine AS l
        WHERE l.ClaimId = @ClaimId AND l.LineStatus = 'P' AND NOT EXISTS (SELECT 1 FROM #price AS p WHERE p.Gtin = l.Gtin);
    END TRY
    BEGIN CATCH
        DECLARE @msg nvarchar(400) = LEFT(N'Price book not available: ' + ERROR_MESSAGE(), 400);
        INSERT rebate.ClaimIssue (ClaimId, LineNumber, Message) VALUES (@ClaimId, 0, @msg);
        DECLARE @eventText nvarchar(4000) = N'Claim ' + CAST(@ClaimId AS nvarchar(12)) + N': ' + @msg;
        EXEC rebate.RaiseEvent 2201, N'W', @eventText;
    END CATCH
END

UPDATE rebate.ClaimLine SET LineStatus = 'K' WHERE ClaimId = @ClaimId AND LineStatus = 'P';

UPDATE c
SET LineCount     = s.LineCount,
    TotalQuantity = s.Quantity,
    TotalAmount   = s.Amount,
    WarningCount  = s.Warnings,
    RejectedCount = s.Rejected,
    Status        = CASE WHEN s.LineCount > 0 AND s.Rejected = s.LineCount THEN 'X' ELSE 'V' END,
    ValidatedAt   = SYSDATETIME()
FROM rebate.Claim AS c
CROSS APPLY (
    SELECT COUNT(*) AS LineCount,
           ISNULL(SUM(CASE WHEN LineStatus <> 'X' THEN Quantity ELSE 0 END), 0) AS Quantity,
           ISNULL(SUM(CASE WHEN LineStatus <> 'X' THEN NetAmount ELSE 0 END), 0) AS Amount,
           SUM(CASE WHEN LineStatus = 'W' THEN 1 ELSE 0 END) AS Warnings,
           SUM(CASE WHEN LineStatus = 'X' THEN 1 ELSE 0 END) AS Rejected
    FROM rebate.ClaimLine WHERE ClaimId = c.ClaimId
) AS s
WHERE c.ClaimId = @ClaimId;
GO

----------------------------------------------------------------------------------------------
-- Claim intake (API, portal, drop folder, workbooks)
----------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE rebate.SubmitClaim
    @DistributorId int,
    @Period int,
    @Source varchar(10),
    @SourceRef nvarchar(400),
    @SubmittedBy nvarchar(128),
    @Lines rebate.ClaimLineTable READONLY,
    @ClaimId int OUTPUT
AS
SET NOCOUNT, XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM rebate.Distributor WHERE DistributorId = @DistributorId AND IsActive = 1)
    THROW 50001, N'Distributor is not active.', 1;
IF @Period IS NULL OR @Period / 100 NOT BETWEEN 2000 AND 2099 OR @Period % 100 NOT BETWEEN 1 AND 12
    THROW 50002, N'Period must be yyyyMM.', 1;
IF NOT EXISTS (SELECT 1 FROM @Lines)
    THROW 50003, N'A claim needs at least one line.', 1;

BEGIN TRANSACTION;

INSERT rebate.Claim (DistributorId, Period, Source, SourceRef, ReceivedBy)
VALUES (@DistributorId, @Period, @Source, @SourceRef, ISNULL(@SubmittedBy, ORIGINAL_LOGIN()));
SET @ClaimId = CAST(SCOPE_IDENTITY() AS int);

INSERT rebate.ClaimLine (ClaimId, LineNumber, InvoiceNo, InvoiceDate, CustomerNo, Gtin, Quantity, NetAmount)
SELECT @ClaimId, LineNumber, LTRIM(RTRIM(InvoiceNo)), InvoiceDate, LTRIM(RTRIM(CustomerNo)),
       RIGHT(REPLICATE('0', 14) + LTRIM(RTRIM(Gtin)), 14), Quantity, NetAmount
FROM @Lines;

DECLARE @detail nvarchar(2000) = @Source + N' ' + ISNULL(@SourceRef, N'') + N', ' + CAST(@@ROWCOUNT AS nvarchar(12)) + N' lines';
DECLARE @id nvarchar(40) = CAST(@ClaimId AS nvarchar(40));
EXEC rebate.WriteAudit @SubmittedBy, 'ClaimReceived', 'Claim', @id, @detail;

COMMIT;

EXEC rebate.ValidateClaim @ClaimId;
GO

CREATE OR ALTER PROCEDURE rebate.ImportClaimFile
    @Path nvarchar(400),
    @CodePage int = 850,
    @SubmittedBy nvarchar(128) = NULL,
    @ClaimId int OUTPUT
AS
SET NOCOUNT, XACT_ABORT ON;

DECLARE @fileName nvarchar(260) = RIGHT(@Path, CHARINDEX(N'\', REVERSE(@Path) + N'\') - 1);

IF EXISTS (SELECT 1 FROM rebate.Claim WHERE Source = 'FILE' AND SourceRef = @fileName AND Status <> 'C')
    THROW 50013, N'This file has already been imported.', 1;

SELECT * INTO #file FROM rebate.clr_ReadClaimFile(@Path, @CodePage);

DECLARE @code nvarchar(20), @period int, @format nvarchar(20);
SELECT @code = DistributorCode, @period = Period, @format = Message FROM #file WHERE Kind = 'H';

IF NULLIF(@code, N'') IS NULL OR @period IS NULL
BEGIN
    DECLARE @first nvarchar(400) = (SELECT TOP (1) Message FROM #file WHERE Kind = 'E' ORDER BY LineNumber);
    DECLARE @headerMsg nvarchar(2048) = N'File has no readable header (' + ISNULL(@format, N'?') + N'): ' + ISNULL(@first, N'no details');
    THROW 50010, @headerMsg, 1;
END

DECLARE @DistributorId int = (SELECT DistributorId FROM rebate.Distributor WHERE Code = @code);
IF @DistributorId IS NULL
BEGIN
    DECLARE @unknownMsg nvarchar(2048) = N'Unknown distributor code ' + @code + N'.';
    THROW 50011, @unknownMsg, 1;
END

IF NOT EXISTS (SELECT 1 FROM #file WHERE Kind = 'L')
    THROW 50012, N'The file has no readable lines.', 1;

DECLARE @lines rebate.ClaimLineTable;
INSERT @lines (LineNumber, InvoiceNo, InvoiceDate, CustomerNo, Gtin, Quantity, NetAmount)
SELECT LineNumber, InvoiceNo, InvoiceDate, ISNULL(CustomerNo, N''), Gtin, Quantity, NetAmount
FROM #file WHERE Kind = 'L';

DECLARE @by nvarchar(128) = ISNULL(@SubmittedBy, ORIGINAL_LOGIN());
EXEC rebate.SubmitClaim @DistributorId, @period, 'FILE', @fileName, @by, @lines, @ClaimId OUTPUT;

INSERT rebate.ClaimIssue (ClaimId, LineNumber, Message)
SELECT @ClaimId, LineNumber, Message FROM #file WHERE Kind = 'E';

UPDATE rebate.Claim SET RejectedCount = RejectedCount + @@ROWCOUNT WHERE ClaimId = @ClaimId;
GO

CREATE OR ALTER PROCEDURE rebate.ImportDropFolder @MaxFiles int = 50
AS
SET NOCOUNT ON;

DECLARE @folder nvarchar(400) = rebate.GetSetting('DropFolder'),
        @pattern nvarchar(100) = ISNULL(rebate.GetSetting('DropFilePattern'), N'*.*'),
        @codePage int = TRY_CONVERT(int, rebate.GetSetting('DropCodePage')),
        @processed nvarchar(400) = rebate.GetSetting('ProcessedFolder'),
        @rejected nvarchar(400) = rebate.GetSetting('RejectedFolder');

DECLARE @result TABLE (FileName nvarchar(260), ClaimId int NULL, Outcome varchar(10), Message nvarchar(2000) NULL);
DECLARE @files TABLE (FullPath nvarchar(400), FileName nvarchar(260), LastWriteUtc datetime2(0));

-- Skip files still being written (locked or changed in the last 30 seconds).
INSERT @files (FullPath, FileName, LastWriteUtc)
SELECT TOP (@MaxFiles) FullPath, FileName, LastWriteUtc
FROM rebate.clr_ListFiles(@folder, @pattern)
WHERE IsLocked = 0 AND SizeBytes > 0 AND LastWriteUtc < DATEADD(SECOND, -30, SYSUTCDATETIME())
ORDER BY LastWriteUtc;

DECLARE @path nvarchar(400), @name nvarchar(260), @claimId int, @newPath nvarchar(400), @msg nvarchar(2000);
DECLARE f CURSOR LOCAL FAST_FORWARD FOR SELECT FullPath, FileName FROM @files ORDER BY LastWriteUtc;
OPEN f;
FETCH NEXT FROM f INTO @path, @name;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @claimId = NULL;
    BEGIN TRY
        EXEC rebate.ImportClaimFile @path, @codePage, NULL, @claimId OUTPUT;
        EXEC rebate.clr_MoveFile @path, @processed, @newPath OUTPUT;
        INSERT @result VALUES (@name, @claimId, 'IMPORTED', NULL);
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        SET @msg = ERROR_MESSAGE();
        INSERT @result VALUES (@name, @claimId, 'REJECTED', @msg);
        EXEC rebate.WriteAudit NULL, 'FileRejected', 'File', NULL, @msg;
        DECLARE @eventText nvarchar(4000) = N'Sell-out file ' + @path + N' was rejected: ' + @msg;
        EXEC rebate.RaiseEvent 2101, N'E', @eventText;
        BEGIN TRY
            IF @claimId IS NULL
                EXEC rebate.clr_MoveFile @path, @rejected, @newPath OUTPUT;
        END TRY
        BEGIN CATCH
            -- Leave the file in place; the next run reports it again.
        END CATCH
    END CATCH
    FETCH NEXT FROM f INTO @path, @name;
END
CLOSE f;
DEALLOCATE f;

SELECT FileName, ClaimId, Outcome, Message FROM @result;
GO

CREATE OR ALTER PROCEDURE rebate.ProcessWorkbookStage @FileName nvarchar(260), @SubmittedBy nvarchar(128) = NULL
AS
SET NOCOUNT, XACT_ABORT ON;

DECLARE @rows TABLE
(
    RowNo int PRIMARY KEY, DistributorId int NULL, Code nvarchar(255) NULL, Period int NULL, InvoiceNo nvarchar(20) NULL,
    InvoiceDate date NULL, CustomerNo nvarchar(20) NULL, Gtin nvarchar(14) NULL, Quantity decimal(19,4) NULL, NetAmount decimal(19,2) NULL,
    Problem nvarchar(200) NULL, Attached tinyint NOT NULL DEFAULT (0)
);

INSERT @rows (RowNo, DistributorId, Code, Period, InvoiceNo, InvoiceDate, CustomerNo, Gtin, Quantity, NetAmount)
SELECT s.RowNo, d.DistributorId, LTRIM(RTRIM(s.DistributorCode)),
       TRY_CONVERT(int, TRY_CONVERT(float, s.Period)),
       LEFT(LTRIM(RTRIM(s.InvoiceNo)), 20),
       COALESCE(TRY_CONVERT(date, s.InvoiceDate, 23), TRY_CONVERT(date, s.InvoiceDate, 104), TRY_CONVERT(date, s.InvoiceDate)),
       LEFT(LTRIM(RTRIM(s.CustomerNo)), 20),
       LEFT(LTRIM(RTRIM(REPLACE(s.Gtin, N'.0', N''))), 14),
       TRY_CONVERT(decimal(19,4), TRY_CONVERT(float, s.Quantity)),
       TRY_CONVERT(decimal(19,2), TRY_CONVERT(float, s.NetAmount))
FROM stage.SellOutWorkbook AS s
LEFT JOIN rebate.Distributor AS d ON d.Code = LTRIM(RTRIM(s.DistributorCode))
WHERE s.FileName = @FileName
  AND NOT (s.DistributorCode IS NULL AND s.InvoiceNo IS NULL AND s.Gtin IS NULL);

UPDATE @rows SET Problem = CASE
    WHEN DistributorId IS NULL THEN N'Unknown distributor code.'
    WHEN Period IS NULL OR Period / 100 NOT BETWEEN 2000 AND 2099 OR Period % 100 NOT BETWEEN 1 AND 12 THEN N'Period must be yyyyMM.'
    WHEN NULLIF(InvoiceNo, N'') IS NULL THEN N'Invoice number missing.'
    WHEN InvoiceDate IS NULL THEN N'Invoice date is not valid.'
    WHEN NULLIF(Gtin, N'') IS NULL THEN N'GTIN missing.'
    WHEN Quantity IS NULL THEN N'Quantity is not a number.'
    WHEN NetAmount IS NULL THEN N'Net amount is not a number.'
END;

DECLARE @result TABLE (ClaimId int NULL, DistributorCode nvarchar(255) NULL, Period int NULL, Lines int, Message nvarchar(400) NULL);
DECLARE @distributorId int, @period int, @claimId int, @lines rebate.ClaimLineTable, @by nvarchar(128) = ISNULL(@SubmittedBy, ORIGINAL_LOGIN());

DECLARE g CURSOR LOCAL FAST_FORWARD FOR
    SELECT DISTINCT DistributorId, Period FROM @rows WHERE Problem IS NULL;
OPEN g;
FETCH NEXT FROM g INTO @distributorId, @period;
WHILE @@FETCH_STATUS = 0
BEGIN
    DELETE @lines;
    INSERT @lines (LineNumber, InvoiceNo, InvoiceDate, CustomerNo, Gtin, Quantity, NetAmount)
    SELECT RowNo, InvoiceNo, InvoiceDate, ISNULL(CustomerNo, N''), Gtin, Quantity, NetAmount
    FROM @rows WHERE DistributorId = @distributorId AND Period = @period AND Problem IS NULL;

    EXEC rebate.SubmitClaim @distributorId, @period, 'WORKBOOK', @FileName, @by, @lines, @claimId OUTPUT;

    -- Rows that failed checks are reported on the claim of the same distributor and period (or the first one).
    UPDATE r SET Attached = 2
    FROM @rows AS r
    WHERE r.DistributorId = @distributorId AND r.Problem IS NOT NULL AND r.Attached = 0
      AND (r.Period = @period OR r.Period IS NULL
           OR NOT EXISTS (SELECT 1 FROM @rows AS o WHERE o.DistributorId = @distributorId AND o.Period = r.Period AND o.Problem IS NULL));

    INSERT rebate.ClaimIssue (ClaimId, LineNumber, Message)
    SELECT @claimId, RowNo, Problem FROM @rows WHERE Attached = 2;
    UPDATE rebate.Claim SET RejectedCount = RejectedCount + @@ROWCOUNT WHERE ClaimId = @claimId;
    UPDATE @rows SET Attached = 1 WHERE Attached = 2;

    INSERT @result (ClaimId, DistributorCode, Period, Lines)
    SELECT @claimId, Code, @period, COUNT(*) FROM @lines CROSS JOIN (SELECT Code FROM rebate.Distributor WHERE DistributorId = @distributorId) AS d GROUP BY Code;

    FETCH NEXT FROM g INTO @distributorId, @period;
END
CLOSE g;
DEALLOCATE g;

INSERT @result (ClaimId, DistributorCode, Period, Lines, Message)
SELECT NULL, Code, Period, COUNT(*), MIN(Problem)
FROM @rows
WHERE Problem IS NOT NULL AND Attached = 0
GROUP BY Code, Period;

DELETE stage.SellOutWorkbook WHERE FileName = @FileName;

SELECT ClaimId, DistributorCode, Period, Lines, Message FROM @result;
GO

CREATE OR ALTER PROCEDURE rebate.ImportSellOutWorkbook @Path nvarchar(400), @SubmittedBy nvarchar(128) = NULL
AS
SET NOCOUNT ON;

DECLARE @fileName nvarchar(260) = RIGHT(@Path, CHARINDEX(N'\', REVERSE(@Path) + N'\') - 1);
DECLARE @provider nvarchar(100) = ISNULL(rebate.GetSetting('WorkbookProvider'), N'Microsoft.ACE.OLEDB.16.0');

IF EXISTS (SELECT 1 FROM rebate.Claim WHERE Source = 'WORKBOOK' AND SourceRef = @fileName AND Status <> 'C')
    THROW 50013, N'This workbook has already been imported.', 1;

DELETE stage.SellOutWorkbook WHERE FileName = @fileName;

-- IMEX=1 reads mixed columns as text; the stage table keeps everything as text until ProcessWorkbookStage.
DECLARE @sql nvarchar(max) =
      N'INSERT stage.SellOutWorkbook (FileName, RowNo, DistributorCode, Period, InvoiceNo, InvoiceDate, CustomerNo, Gtin, Quantity, NetAmount)'
    + N' SELECT @file, 1 + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),'
    + N' CAST(DistributorCode AS nvarchar(255)), CAST(Period AS nvarchar(255)), CAST(InvoiceNo AS nvarchar(255)), CAST(InvoiceDate AS nvarchar(255)),'
    + N' CAST(CustomerNo AS nvarchar(255)), CAST(Gtin AS nvarchar(255)), CAST(Quantity AS nvarchar(255)), CAST(NetAmount AS nvarchar(255))'
    + N' FROM OPENROWSET(''' + REPLACE(@provider, N'''', N'''''') + N''', ''Excel 12.0 Xml;HDR=YES;IMEX=1;Database='
    + REPLACE(@Path, N'''', N'''''') + N''', ''SELECT * FROM [SellOut$]'');';

EXEC sys.sp_executesql @sql, N'@file nvarchar(260)', @file = @fileName;

EXEC rebate.ProcessWorkbookStage @fileName, @SubmittedBy;
GO

-- Picks up workbooks from the workbook folder when the SSIS import is not installed.
CREATE OR ALTER PROCEDURE rebate.ImportWorkbookFolder @MaxFiles int = 20
AS
SET NOCOUNT ON;
DECLARE @folder nvarchar(400) = rebate.GetSetting('WorkbookFolder'),
        @processed nvarchar(400) = rebate.GetSetting('ProcessedFolder'),
        @rejected nvarchar(400) = rebate.GetSetting('RejectedFolder');
DECLARE @path nvarchar(400), @newPath nvarchar(400), @msg nvarchar(2000);

DECLARE w CURSOR LOCAL FAST_FORWARD FOR
    SELECT TOP (@MaxFiles) FullPath FROM rebate.clr_ListFiles(@folder, N'*.xlsx')
    WHERE IsLocked = 0 AND FileName NOT LIKE N'~$%' ORDER BY LastWriteUtc;
OPEN w;
FETCH NEXT FROM w INTO @path;
WHILE @@FETCH_STATUS = 0
BEGIN
    BEGIN TRY
        EXEC rebate.ImportSellOutWorkbook @path, NULL;
        EXEC rebate.clr_MoveFile @path, @processed, @newPath OUTPUT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        SET @msg = N'Workbook ' + @path + N' was rejected: ' + ERROR_MESSAGE();
        EXEC rebate.WriteAudit NULL, 'FileRejected', 'Workbook', NULL, @msg;
        EXEC rebate.RaiseEvent 2102, N'E', @msg;
        BEGIN TRY
            EXEC rebate.clr_MoveFile @path, @rejected, @newPath OUTPUT;
        END TRY
        BEGIN CATCH
        END CATCH
    END CATCH
    FETCH NEXT FROM w INTO @path;
END
CLOSE w;
DEALLOCATE w;
GO

CREATE OR ALTER PROCEDURE rebate.DecideClaim
    @ClaimId int, @RowVer binary(8), @Approve bit, @Actor nvarchar(128), @Note nvarchar(400) = NULL
AS
SET NOCOUNT, XACT_ABORT ON;

UPDATE rebate.Claim
SET Status = CASE WHEN @Approve = 1 THEN 'A' ELSE 'X' END,
    DecidedBy = @Actor, DecidedAt = SYSDATETIME(), DecisionNote = @Note
WHERE ClaimId = @ClaimId AND RowVer = @RowVer AND Status = 'V';

IF @@ROWCOUNT = 0
BEGIN
    IF NOT EXISTS (SELECT 1 FROM rebate.Claim WHERE ClaimId = @ClaimId AND Status = 'V')
        THROW 50021, N'Only validated claims can be approved or rejected.', 1;
    THROW 50020, N'The claim was changed by someone else. Reload and try again.', 1;
END

DECLARE @id nvarchar(40) = CAST(@ClaimId AS nvarchar(40));
DECLARE @action varchar(40) = CASE WHEN @Approve = 1 THEN 'ClaimApproved' ELSE 'ClaimRejected' END;
EXEC rebate.WriteAudit @Actor, @action, 'Claim', @id, @Note;
GO
