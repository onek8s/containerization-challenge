/*
    BAMPrimaryImport database: reporting views and procedures over the OrderLifecycle and InterchangeTracking
    activities (bam\*.xml, deployed with bm.exe before this script), read by the tracking portal, the gateway's
    order status operation and the SLA monitor.

    Partner master data is read from the integration database through synonyms in schema b2b.
    sqlcmd variable: IntegrationDatabase (sqlcmd -v IntegrationDatabase=VanArsdelIntegration).
*/
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.bam_OrderLifecycle_AllInstances', N'V') IS NULL
    RAISERROR (N'BAM activity OrderLifecycle is not deployed (bm.exe deploy-all -DefinitionFile:bam\OrderLifecycle.xml).', 16, 1);
IF OBJECT_ID(N'dbo.bam_InterchangeTracking_AllInstances', N'V') IS NULL
    RAISERROR (N'BAM activity InterchangeTracking is not deployed (bm.exe deploy-all -DefinitionFile:bam\InterchangeTracking.xml).', 16, 1);
GO

IF SCHEMA_ID(N'rpt') IS NULL EXEC (N'CREATE SCHEMA rpt AUTHORIZATION dbo;');
IF SCHEMA_ID(N'b2b') IS NULL EXEC (N'CREATE SCHEMA b2b AUTHORIZATION dbo;');
GO

IF OBJECT_ID(N'b2b.TradingPartner', N'SN') IS NOT NULL DROP SYNONYM b2b.TradingPartner;
CREATE SYNONYM b2b.TradingPartner FOR [$(IntegrationDatabase)].dbo.TradingPartner;
GO

/*
    One row per order. A business day in BAM is UTC; the portal converts to the viewer's time zone.
    Completed = invoiced or rejected (OrderLifecycle.IsCompleted in the domain library).
*/
CREATE OR ALTER VIEW rpt.vOrderLifecycle
AS
SELECT  CAST(a.ActivityID AS nvarchar(128))         AS ActivityID,
        a.PartnerId,
        a.OrderNumber,
        a.ErpOrderNumber,
        CAST(a.OrderValue AS decimal(18,2))         AS OrderValue,
        ISNULL(a.LineCount, 0)                      AS LineCount,
        a.Currency,
        a.Status,
        a.ReceivedUtc,
        a.ValidatedUtc,
        a.CreditHoldUtc,
        a.ReleasedUtc,
        a.SentToErpUtc,
        a.ConfirmedUtc,
        a.DespatchedUtc,
        a.AsnSentUtc,
        a.InvoicedUtc,
        a.RejectedUtc,
        a.RejectReason,
        a.HoldReason,
        a.DespatchNumber,
        a.InvoiceNumber,
        CAST(CASE WHEN a.InvoicedUtc IS NOT NULL OR a.RejectedUtc IS NOT NULL THEN 1 ELSE 0 END AS bit) AS IsCompleted
FROM    dbo.bam_OrderLifecycle_AllInstances AS a;
GO

/* Last inbound interchange per partner, including partners that have not sent anything yet. */
CREATE OR ALTER VIEW rpt.vPartnerLastReceived
AS
SELECT  p.PartnerId,
        r.LastReceivedUtc,
        p.ExpectsDailyTraffic
FROM    b2b.TradingPartner AS p
LEFT JOIN
(
        SELECT  t.PartnerId, MAX(t.TrackedUtc) AS LastReceivedUtc
        FROM    dbo.bam_InterchangeTracking_AllInstances AS t
        WHERE   t.Direction = N'Inbound'
        GROUP BY t.PartnerId
) AS r ON r.PartnerId = p.PartnerId
WHERE   p.Active = 1;
GO

/*
    Portal search. @Status takes the portal's filter values (Received, Validated, CreditHold, SentToErp,
    Confirmed, Despatched, Invoiced, Rejected); BAM keeps the milestone names, so Confirmed = ConfirmedByErp.
*/
CREATE OR ALTER PROCEDURE rpt.usp_SearchOrderLifecycle
    @PartnerId      nvarchar(20) = NULL,
    @OrderNumber    nvarchar(35) = NULL,
    @ErpOrderNumber nvarchar(20) = NULL,
    @FromUtc        datetime2(3) = NULL,
    @ToUtc          datetime2(3) = NULL,
    @Status         nvarchar(20) = NULL,
    @MaxRows        int = 500
AS
BEGIN
    SET NOCOUNT ON;
    IF @Status = N'Confirmed' SET @Status = N'ConfirmedByErp';
    IF @MaxRows IS NULL OR @MaxRows <= 0 OR @MaxRows > 5000 SET @MaxRows = 500;

    SELECT TOP (@MaxRows) *
    FROM rpt.vOrderLifecycle
    WHERE (@PartnerId IS NULL OR @PartnerId = N'' OR PartnerId = @PartnerId)
      AND (@OrderNumber IS NULL OR @OrderNumber = N'' OR OrderNumber LIKE REPLACE(@OrderNumber, N'*', N'%'))
      AND (@ErpOrderNumber IS NULL OR @ErpOrderNumber = N'' OR ErpOrderNumber = @ErpOrderNumber
           OR ErpOrderNumber = RIGHT(REPLICATE(N'0', 10) + @ErpOrderNumber, 10))
      AND (@FromUtc IS NULL OR ReceivedUtc >= @FromUtc)
      AND (@ToUtc IS NULL OR ReceivedUtc < @ToUtc)
      AND (@Status IS NULL OR @Status = N'' OR Status = @Status)
    ORDER BY ReceivedUtc DESC
    OPTION (RECOMPILE);
END
GO

/* Interchanges per UTC day and message kind (one column per kind, so the Excel export follows new kinds). */
CREATE OR ALTER PROCEDURE rpt.usp_DailyDocumentVolumes
    @FromUtc    datetime2(3),
    @ToUtc      datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @columns nvarchar(max), @sql nvarchar(max);

    SELECT  @columns = STUFF((SELECT N',' + QUOTENAME(k.Kind) FROM
                (SELECT DISTINCT ISNULL(NULLIF(MessageKind, N''), N'Other') + N' ' + Direction AS Kind
                 FROM dbo.bam_InterchangeTracking_AllInstances
                 WHERE TrackedUtc >= @FromUtc AND TrackedUtc < @ToUtc) AS k
                ORDER BY k.Kind FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 1, N'');

    IF @columns IS NULL
    BEGIN
        SELECT CAST(NULL AS date) AS [Day] WHERE 1 = 0;
        RETURN;
    END

    SET @sql = N'
SELECT [Day], ' + @columns + N'
FROM
(
    SELECT CAST(TrackedUtc AS date) AS [Day], ISNULL(NULLIF(MessageKind, N''''), N''Other'') + N'' '' + Direction AS Kind, ActivityID
    FROM dbo.bam_InterchangeTracking_AllInstances
    WHERE TrackedUtc >= @FromUtc AND TrackedUtc < @ToUtc
) AS s
PIVOT (COUNT(ActivityID) FOR Kind IN (' + @columns + N')) AS p
ORDER BY [Day];';

    EXEC sys.sp_executesql @sql, N'@FromUtc datetime2(3), @ToUtc datetime2(3)', @FromUtc, @ToUtc;
END
GO
