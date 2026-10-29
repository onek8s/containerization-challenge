:on error exit
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
USE [$(DatabaseName)];
GO

----------------------------------------------------------------------------------------------
-- Accruals: year-to-date rebate per agreement, booked as the difference to earlier periods.
----------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE rebate.CalculateAccruals @Period int = NULL
AS
SET NOCOUNT, XACT_ABORT ON;

IF @Period IS NULL
    SET @Period = YEAR(DATEADD(MONTH, -1, GETDATE())) * 100 + MONTH(DATEADD(MONTH, -1, GETDATE()));

DECLARE @PeriodStart date = DATEFROMPARTS(@Period / 100, @Period % 100, 1);
DECLARE @PeriodEnd date = EOMONTH(@PeriodStart);

CREATE TABLE #measure
(
    AgreementId int PRIMARY KEY,
    Quantity decimal(19,4) NOT NULL, NetAmount decimal(19,2) NOT NULL,
    YtdQuantity decimal(19,4) NOT NULL, YtdNetAmount decimal(19,2) NOT NULL
);

INSERT #measure (AgreementId, Quantity, NetAmount, YtdQuantity, YtdNetAmount)
SELECT a.AgreementId,
       ISNULL(SUM(CASE WHEN c.Period = @Period THEN l.Quantity END), 0),
       ISNULL(SUM(CASE WHEN c.Period = @Period THEN l.NetAmount END), 0),
       ISNULL(SUM(l.Quantity), 0),
       ISNULL(SUM(l.NetAmount), 0)
FROM rebate.Agreement AS a
LEFT JOIN rebate.Claim AS c
       ON c.DistributorId = a.DistributorId AND c.Status = 'A'
      AND c.Period BETWEEN YEAR(a.ValidFrom) * 100 + MONTH(a.ValidFrom) AND @Period
LEFT JOIN rebate.ClaimLine AS l
       ON l.ClaimId = c.ClaimId AND l.LineStatus IN ('K', 'W')
      AND l.InvoiceDate BETWEEN a.ValidFrom AND a.ValidTo
      AND (a.Category IS NULL OR EXISTS (SELECT 1 FROM rebate.Product AS p WHERE p.Gtin = l.Gtin AND p.Category = a.Category))
WHERE a.Status = 'A' AND a.ValidFrom <= @PeriodEnd AND a.ValidTo >= @PeriodStart
GROUP BY a.AgreementId;

WITH calc AS
(
    SELECT m.AgreementId, m.Quantity, m.NetAmount, m.YtdQuantity, m.YtdNetAmount,
           a.Tiers.RateFor(CASE a.Basis WHEN 'Q' THEN m.YtdQuantity ELSE m.YtdNetAmount END) AS RatePercent,
           a.Tiers.ToNextTier(CASE a.Basis WHEN 'Q' THEN m.YtdQuantity ELSE m.YtdNetAmount END) AS ToNextTier,
           CASE WHEN a.CapAmount IS NOT NULL AND a.Tiers.RebateFor(CASE a.Basis WHEN 'Q' THEN m.YtdQuantity ELSE m.YtdNetAmount END, m.YtdNetAmount) > a.CapAmount
                THEN a.CapAmount
                ELSE a.Tiers.RebateFor(CASE a.Basis WHEN 'Q' THEN m.YtdQuantity ELSE m.YtdNetAmount END, m.YtdNetAmount)
           END AS YtdRebate,
           ISNULL((SELECT SUM(e.AccruedAmount) FROM rebate.Accrual AS e WHERE e.AgreementId = m.AgreementId AND e.Period < @Period), 0) AS Earlier
    FROM #measure AS m
    JOIN rebate.Agreement AS a ON a.AgreementId = m.AgreementId
)
MERGE rebate.Accrual AS t
USING (SELECT AgreementId, Quantity, NetAmount, YtdQuantity, YtdNetAmount, RatePercent, ToNextTier, YtdRebate,
              CAST(YtdRebate - Earlier AS decimal(19,2)) AS AccruedAmount
       FROM calc) AS s
ON t.AgreementId = s.AgreementId AND t.Period = @Period
WHEN MATCHED AND t.StatementId IS NULL THEN
    UPDATE SET Quantity = s.Quantity, NetAmount = s.NetAmount, YtdQuantity = s.YtdQuantity, YtdNetAmount = s.YtdNetAmount,
               RatePercent = s.RatePercent, ToNextTier = s.ToNextTier, YtdRebate = s.YtdRebate, AccruedAmount = s.AccruedAmount,
               CalculatedAt = SYSDATETIME()
WHEN NOT MATCHED THEN
    INSERT (AgreementId, Period, Quantity, NetAmount, YtdQuantity, YtdNetAmount, RatePercent, ToNextTier, YtdRebate, AccruedAmount)
    VALUES (s.AgreementId, @Period, s.Quantity, s.NetAmount, s.YtdQuantity, s.YtdNetAmount, s.RatePercent, s.ToNextTier, s.YtdRebate, s.AccruedAmount);

DECLARE @detail nvarchar(2000) = CAST(@@ROWCOUNT AS nvarchar(12)) + N' agreements';
DECLARE @id nvarchar(40) = CAST(@Period AS nvarchar(40));
EXEC rebate.WriteAudit NULL, 'AccrualsCalculated', 'Period', @id, @detail;

SELECT a.AgreementNo, x.Period, x.Quantity, x.NetAmount, x.YtdNetAmount, x.RatePercent, x.YtdRebate, x.AccruedAmount, x.ToNextTier
FROM rebate.Accrual AS x JOIN rebate.Agreement AS a ON a.AgreementId = x.AgreementId
WHERE x.Period = @Period
ORDER BY a.AgreementNo;
GO

----------------------------------------------------------------------------------------------
-- Quarterly statements: one Service Broker conversation per distributor statement.
----------------------------------------------------------------------------------------------
CREATE OR ALTER FUNCTION rebate.QuarterPeriods (@Quarter char(6))
RETURNS TABLE
AS RETURN
    SELECT CAST(LEFT(@Quarter, 4) AS int) * 100 + (CAST(RIGHT(@Quarter, 1) AS int) - 1) * 3 + m.n AS Period
    FROM (VALUES (1), (2), (3)) AS m (n);
GO

CREATE OR ALTER PROCEDURE rebate.RequestStatements
    @Quarter char(6), @RequestedBy nvarchar(128) = NULL, @DistributorId int = NULL
AS
SET NOCOUNT, XACT_ABORT ON;

IF @Quarter NOT LIKE '20[0-9][0-9]Q[1-4]'
    THROW 50040, N'Quarter must be yyyyQn.', 1;

DECLARE @by nvarchar(128) = ISNULL(@RequestedBy, ORIGINAL_LOGIN());
DECLARE @requested TABLE (StatementId int, DistributorId int, Amount decimal(19,2));
DECLARE @dist int, @amount decimal(19,2), @statementId int, @h uniqueidentifier, @body xml;

DECLARE d CURSOR LOCAL FAST_FORWARD FOR
    SELECT a.DistributorId, SUM(x.AccruedAmount)
    FROM rebate.Accrual AS x
    JOIN rebate.Agreement AS a ON a.AgreementId = x.AgreementId
    WHERE x.Period IN (SELECT Period FROM rebate.QuarterPeriods(@Quarter))
      AND x.StatementId IS NULL
      AND (@DistributorId IS NULL OR a.DistributorId = @DistributorId)
      AND NOT EXISTS (SELECT 1 FROM rebate.Statement AS s WHERE s.DistributorId = a.DistributorId AND s.Quarter = @Quarter AND s.Status <> 'F')
    GROUP BY a.DistributorId
    HAVING SUM(x.AccruedAmount) <> 0;
OPEN d;
FETCH NEXT FROM d INTO @dist, @amount;
WHILE @@FETCH_STATUS = 0
BEGIN
    BEGIN TRANSACTION;

    INSERT rebate.Statement (DistributorId, Quarter, Amount, RequestedBy) VALUES (@dist, @Quarter, @amount, @by);
    SET @statementId = CAST(SCOPE_IDENTITY() AS int);

    UPDATE x SET StatementId = @statementId
    FROM rebate.Accrual AS x JOIN rebate.Agreement AS a ON a.AgreementId = x.AgreementId
    WHERE a.DistributorId = @dist AND x.StatementId IS NULL
      AND x.Period IN (SELECT Period FROM rebate.QuarterPeriods(@Quarter));

    BEGIN DIALOG CONVERSATION @h
        FROM SERVICE [//contosofoods.com/TradeRebates/StatementRequester]
        TO SERVICE N'//contosofoods.com/TradeRebates/StatementService', N'CURRENT DATABASE'
        ON CONTRACT [//contosofoods.com/TradeRebates/StatementContract]
        WITH ENCRYPTION = OFF, LIFETIME = 86400;

    SET @body = (SELECT @statementId AS [@StatementId], @Quarter AS [@Quarter], @by AS [@RequestedBy]
                 FOR XML PATH (N'StatementRequest'), TYPE);

    SEND ON CONVERSATION @h MESSAGE TYPE [//contosofoods.com/TradeRebates/StatementRequest] (@body);

    UPDATE rebate.Statement SET ConversationHandle = @h WHERE StatementId = @statementId;

    DECLARE @id nvarchar(40) = CAST(@statementId AS nvarchar(40));
    DECLARE @detail nvarchar(2000) = @Quarter + N' ' + FORMAT(@amount, N'0.00');
    EXEC rebate.WriteAudit @by, 'StatementRequested', 'Statement', @id, @detail;

    COMMIT;

    INSERT @requested VALUES (@statementId, @dist, @amount);
    FETCH NEXT FROM d INTO @dist, @amount;
END
CLOSE d;
DEALLOCATE d;

SELECT r.StatementId, d.Code, d.Name, r.Amount
FROM @requested AS r JOIN rebate.Distributor AS d ON d.DistributorId = r.DistributorId
ORDER BY d.Code;
GO

CREATE OR ALTER PROCEDURE rebate.GetStatement @StatementId int
AS
SET NOCOUNT ON;

SELECT s.StatementId, s.Quarter, s.Amount, s.Status, s.RequestedBy, s.RequestedAt, s.FilePath, s.ErpDocumentNo, s.CompletedAt, s.ErrorMessage,
       d.DistributorId, d.Code AS DistributorCode, d.Name AS DistributorName, d.ErpVendorNo, d.Currency, d.StatementFolder,
       d.StatementEmail, d.BankAccountMasked
FROM rebate.Statement AS s
JOIN rebate.Distributor AS d ON d.DistributorId = s.DistributorId
WHERE s.StatementId = @StatementId;

SELECT a.AgreementNo, a.Description, a.Basis, a.TiersText, x.Period, x.Quantity, x.NetAmount, x.YtdQuantity, x.YtdNetAmount,
       x.RatePercent, x.YtdRebate, x.AccruedAmount, x.ToNextTier
FROM rebate.Accrual AS x
JOIN rebate.Agreement AS a ON a.AgreementId = x.AgreementId
WHERE x.StatementId = @StatementId
ORDER BY a.AgreementNo, x.Period;
GO

CREATE OR ALTER PROCEDURE rebate.CompleteStatement @StatementId int, @FilePath nvarchar(400), @Actor nvarchar(128) = NULL
AS
SET NOCOUNT, XACT_ABORT ON;

DECLARE @amount decimal(19,2), @vendor nvarchar(20), @currency char(3), @quarter char(6), @code nvarchar(20), @status char(1);
SELECT @amount = s.Amount, @vendor = d.ErpVendorNo, @currency = d.Currency, @quarter = s.Quarter, @code = d.Code, @status = s.Status
FROM rebate.Statement AS s JOIN rebate.Distributor AS d ON d.DistributorId = s.DistributorId
WHERE s.StatementId = @StatementId;

IF @status IS NULL
    THROW 50041, N'Statement not found.', 1;
IF @status NOT IN ('Q', 'R')
    THROW 50042, N'Statement is already completed.', 1;

UPDATE rebate.Statement SET Status = 'R', FilePath = @FilePath WHERE StatementId = @StatementId;

DECLARE @docNo nvarchar(20) = NULL;
DECLARE @reference nvarchar(40) = N'TR-' + @quarter + N'-' + @code;
DECLARE @text nvarchar(200) = N'Trade rebate ' + @quarter;

IF @amount > 0
BEGIN
    IF rebate.GetSetting('ErpPostingMode') = N'Live'
    BEGIN
        DECLARE @endpoint nvarchar(400) = rebate.GetSetting('ErpEndpoint');
        DECLARE @timeout int = TRY_CONVERT(int, rebate.GetSetting('ErpTimeoutSeconds'));
        EXEC rebate.clr_PostCreditNote @endpoint, @vendor, @amount, @currency, @reference, @text, @timeout, @docNo OUTPUT;
    END
    ELSE
        SET @docNo = N'OFFLINE-' + RIGHT(N'000000' + CAST(@StatementId AS nvarchar(12)), 6);
END

UPDATE rebate.Statement
SET Status = 'P', ErpDocumentNo = @docNo, CompletedAt = SYSDATETIME(), ErrorMessage = NULL
WHERE StatementId = @StatementId;

DECLARE @id nvarchar(40) = CAST(@StatementId AS nvarchar(40));
DECLARE @detail nvarchar(2000) = @reference + N' ' + ISNULL(@docNo, N'(no posting)') + N' ' + @FilePath;
EXEC rebate.WriteAudit @Actor, 'StatementPosted', 'Statement', @id, @detail;

SELECT @StatementId AS StatementId, @docNo AS ErpDocumentNo;
GO

CREATE OR ALTER PROCEDURE rebate.FailStatement @StatementId int, @Error nvarchar(2000), @Actor nvarchar(128) = NULL
AS
SET NOCOUNT, XACT_ABORT ON;

BEGIN TRANSACTION;
UPDATE rebate.Statement SET Status = 'F', ErrorMessage = LEFT(@Error, 2000), CompletedAt = SYSDATETIME()
WHERE StatementId = @StatementId AND Status IN ('Q', 'R');

IF @@ROWCOUNT = 0
BEGIN
    COMMIT;
    RETURN;
END
UPDATE rebate.Accrual SET StatementId = NULL WHERE StatementId = @StatementId;
COMMIT;

DECLARE @id nvarchar(40) = CAST(@StatementId AS nvarchar(40));
EXEC rebate.WriteAudit @Actor, 'StatementFailed', 'Statement', @id, @Error;
DECLARE @eventText nvarchar(4000) = N'Statement ' + @id + N' failed: ' + @Error;
EXEC rebate.RaiseEvent 3101, N'E', @eventText;
GO

-- Initiator side: closes finished conversations and records errors sent back by the statement worker.
CREATE OR ALTER PROCEDURE rebate.StatementReplyActivation
AS
SET NOCOUNT ON;
DECLARE @h uniqueidentifier, @type sysname, @body varbinary(max), @statementId int, @error nvarchar(2000);

WHILE 1 = 1
BEGIN
    SET @h = NULL;
    WAITFOR (
        RECEIVE TOP (1) @h = conversation_handle, @type = message_type_name, @body = message_body
        FROM rebate.StatementInitiatorQueue
    ), TIMEOUT 1000;

    IF @h IS NULL BREAK;

    IF @type = N'http://schemas.microsoft.com/SQL/ServiceBroker/Error'
    BEGIN
        WITH XMLNAMESPACES (DEFAULT N'http://schemas.microsoft.com/SQL/ServiceBroker/Error')
        SELECT @error = CAST(@body AS xml).value(N'(/Error/Description)[1]', N'nvarchar(2000)');

        SELECT @statementId = StatementId FROM rebate.Statement WHERE ConversationHandle = @h;
        IF @statementId IS NOT NULL
            EXEC rebate.FailStatement @statementId, @error, N'Service Broker';
        END CONVERSATION @h;
    END
    ELSE IF @type = N'http://schemas.microsoft.com/SQL/ServiceBroker/EndDialog'
        END CONVERSATION @h;
END
GO

-- Activation for the initiator queue created in 07-service-broker.sql.
ALTER QUEUE rebate.StatementInitiatorQueue
    WITH ACTIVATION (STATUS = ON, PROCEDURE_NAME = rebate.StatementReplyActivation, MAX_QUEUE_READERS = 1, EXECUTE AS OWNER);
GO
