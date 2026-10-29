-- Customer Care stored procedures. Run in the CustomerCare database after 02-schema.sql.
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE care.usp_Case_NextNumber
    @Year int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @next int;
    BEGIN TRANSACTION;

    UPDATE care.CaseNumberSequence WITH (UPDLOCK, HOLDLOCK)
       SET @next = LastValue = LastValue + 1
     WHERE [Year] = @Year;

    IF @@ROWCOUNT = 0
    BEGIN
        SET @next = 1;
        INSERT care.CaseNumberSequence ([Year], LastValue) VALUES (@Year, @next);
    END

    COMMIT;
    SELECT @next;
END
GO

CREATE OR ALTER PROCEDURE care.usp_Case_Search
    @Text         nvarchar(200) = NULL,
    @QueueId      int = NULL,
    @Status       int = NULL,
    @OpenOnly     bit = 0,
    @AssignedTo   nvarchar(100) = NULL,
    @BreachedOnly bit = 0,
    @SortColumn   sysname = N'ResolveDueUtc',
    @SortDesc     bit = 0,
    @PageIndex    int = 0,
    @PageSize     int = 25,
    @TotalRows    int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @now datetime2(0) = SYSUTCDATETIME();
    DECLARE @like nvarchar(202) = CASE WHEN @Text IS NULL THEN NULL ELSE N'%' + @Text + N'%' END;

    IF OBJECT_ID('tempdb..#match') IS NOT NULL DROP TABLE #match;

    SELECT c.Id, c.CaseNumber, c.Subject, cu.Name AS CustomerName, cu.AccountNumber, q.Name AS QueueName,
           c.Priority, c.Status, c.AssignedTo, c.CreatedUtc, c.ResolveDueUtc, c.EscalationLevel
      INTO #match
      FROM care.[Case] c
      JOIN care.Customer cu ON cu.Id = c.CustomerId
      JOIN care.Queue q ON q.Id = c.QueueId
     WHERE (@QueueId IS NULL OR c.QueueId = @QueueId)
       AND (@Status IS NULL OR c.Status = @Status)
       AND (@OpenOnly = 0 OR c.Status < 4)
       AND (@AssignedTo IS NULL OR c.AssignedTo = @AssignedTo)
       AND (@BreachedOnly = 0 OR (c.Status < 4 AND (c.ResolveDueUtc < @now OR (c.FirstResponseUtc IS NULL AND c.ResponseDueUtc < @now))))
       AND (@like IS NULL OR c.CaseNumber LIKE @like OR c.Subject LIKE @like OR cu.Name LIKE @like
            OR cu.AccountNumber = @Text OR c.ExternalReference = @Text);

    SELECT @TotalRows = COUNT(*) FROM #match;

    SELECT *
      FROM #match
     ORDER BY
        CASE WHEN @SortDesc = 0 AND @SortColumn = N'CaseNumber'   THEN CaseNumber END ASC,
        CASE WHEN @SortDesc = 1 AND @SortColumn = N'CaseNumber'   THEN CaseNumber END DESC,
        CASE WHEN @SortDesc = 0 AND @SortColumn = N'Subject'      THEN Subject END ASC,
        CASE WHEN @SortDesc = 1 AND @SortColumn = N'Subject'      THEN Subject END DESC,
        CASE WHEN @SortDesc = 0 AND @SortColumn = N'CustomerName' THEN CustomerName END ASC,
        CASE WHEN @SortDesc = 1 AND @SortColumn = N'CustomerName' THEN CustomerName END DESC,
        CASE WHEN @SortDesc = 0 AND @SortColumn = N'QueueName'    THEN QueueName END ASC,
        CASE WHEN @SortDesc = 1 AND @SortColumn = N'QueueName'    THEN QueueName END DESC,
        CASE WHEN @SortDesc = 0 AND @SortColumn = N'Priority'     THEN Priority END ASC,
        CASE WHEN @SortDesc = 1 AND @SortColumn = N'Priority'     THEN Priority END DESC,
        CASE WHEN @SortDesc = 0 AND @SortColumn = N'Status'       THEN Status END ASC,
        CASE WHEN @SortDesc = 1 AND @SortColumn = N'Status'       THEN Status END DESC,
        CASE WHEN @SortDesc = 0 AND @SortColumn = N'AssignedTo'   THEN AssignedTo END ASC,
        CASE WHEN @SortDesc = 1 AND @SortColumn = N'AssignedTo'   THEN AssignedTo END DESC,
        CASE WHEN @SortDesc = 0 AND @SortColumn = N'CreatedUtc'   THEN CreatedUtc END ASC,
        CASE WHEN @SortDesc = 1 AND @SortColumn = N'CreatedUtc'   THEN CreatedUtc END DESC,
        CASE WHEN @SortDesc = 0 AND @SortColumn = N'ResolveDueUtc' THEN ResolveDueUtc END ASC,
        CASE WHEN @SortDesc = 1 AND @SortColumn = N'ResolveDueUtc' THEN ResolveDueUtc END DESC,
        Id
    OFFSET @PageIndex * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE care.usp_Case_Assign
    @CaseId     int,
    @AssignedTo nvarchar(100),
    @ModifiedBy nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE care.[Case]
       SET AssignedTo = @AssignedTo,
           Status = CASE WHEN @AssignedTo IS NULL THEN 0 WHEN Status = 0 THEN 1 ELSE Status END,
           ModifiedUtc = SYSUTCDATETIME()
     WHERE Id = @CaseId AND Status < 5;

    IF @@ROWCOUNT = 1
        INSERT care.CaseNote (CaseId, Author, Body, IsInternal, CreatedUtc)
        VALUES (@CaseId, @ModifiedBy, N'Assigned to ' + ISNULL(@AssignedTo, N'nobody') + N'.', 1, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE care.usp_Case_SetEscalation
    @CaseId int,
    @Level  int,
    @NotifiedTo nvarchar(400) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE care.[Case] SET EscalationLevel = @Level WHERE Id = @CaseId AND EscalationLevel <> @Level;

    IF @@ROWCOUNT = 1 AND @Level > 0
        INSERT care.EscalationLog (CaseId, [Level], NotifiedTo) VALUES (@CaseId, @Level, @NotifiedTo);
END
GO

CREATE OR ALTER PROCEDURE care.usp_Dashboard_QueueStats
    @DayStartUtc datetime2(0),
    @NowUtc      datetime2(0)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT q.Code AS QueueCode, q.Name AS QueueName,
           COUNT(CASE WHEN c.Status < 4 THEN 1 END) AS OpenCases,
           COUNT(CASE WHEN c.Status < 4 AND c.AssignedTo IS NULL THEN 1 END) AS Unassigned,
           COUNT(CASE WHEN c.Status < 4 AND c.ResolveDueUtc >= @NowUtc
                       AND DATEADD(SECOND, DATEDIFF(SECOND, c.CreatedUtc, c.ResolveDueUtc) * 3 / 4, c.CreatedUtc) <= @NowUtc THEN 1 END) AS AtRisk,
           COUNT(CASE WHEN c.Status < 4 AND (c.ResolveDueUtc < @NowUtc OR (c.FirstResponseUtc IS NULL AND c.ResponseDueUtc < @NowUtc)) THEN 1 END) AS Breached,
           COUNT(CASE WHEN c.CreatedUtc >= @DayStartUtc THEN 1 END) AS CreatedToday,
           COUNT(CASE WHEN c.ResolvedUtc >= @DayStartUtc THEN 1 END) AS ResolvedToday,
           AVG(CASE WHEN c.ResolvedUtc >= DATEADD(DAY, -30, @NowUtc) THEN DATEDIFF(MINUTE, c.CreatedUtc, c.ResolvedUtc) / 60.0 END) AS AvgResolveHours
      FROM care.Queue q
      LEFT JOIN care.[Case] c ON c.QueueId = q.Id
     WHERE q.IsActive = 1
     GROUP BY q.Code, q.Name, q.SortOrder
     ORDER BY q.SortOrder, q.Name;
END
GO

CREATE OR ALTER PROCEDURE care.usp_Dashboard_AgentWorkload
    @NowUtc datetime2(0)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT c.AssignedTo,
           COUNT(CASE WHEN c.Status < 4 THEN 1 END) AS OpenCases,
           COUNT(CASE WHEN c.Status < 4 AND c.ResolveDueUtc < @NowUtc THEN 1 END) AS Breached,
           COUNT(CASE WHEN c.ResolvedUtc >= DATEADD(DAY, -7, @NowUtc) THEN 1 END) AS ResolvedLast7Days
      FROM care.[Case] c
     WHERE c.AssignedTo IS NOT NULL
       AND (c.Status < 4 OR c.ResolvedUtc >= DATEADD(DAY, -7, @NowUtc))
     GROUP BY c.AssignedTo
     ORDER BY OpenCases DESC, c.AssignedTo;
END
GO

CREATE OR ALTER PROCEDURE care.usp_Report_Sla
    @FromUtc datetime2(0),
    @ToUtc   datetime2(0),
    @QueueId int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT c.CaseNumber, q.Name AS QueueName, c.CategoryCode, c.Priority, c.Status, c.AssignedTo,
           c.CreatedUtc, c.ResponseDueUtc, c.FirstResponseUtc, c.ResolveDueUtc, c.ResolvedUtc
      FROM care.[Case] c
      JOIN care.Queue q ON q.Id = c.QueueId
     WHERE c.CreatedUtc >= @FromUtc AND c.CreatedUtc < @ToUtc
       AND (@QueueId IS NULL OR c.QueueId = @QueueId)
     ORDER BY c.CreatedUtc;
END
GO

CREATE OR ALTER PROCEDURE care.usp_Search_Global
    @Text nvarchar(200)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @like nvarchar(202) = N'%' + @Text + N'%';

    SELECT Kind, Id, Title, Detail
      FROM (SELECT TOP (10) 'case' AS Kind, c.Id, c.CaseNumber + N' ' + c.Subject AS Title, cu.Name AS Detail, 1 AS SortGroup
              FROM care.[Case] c JOIN care.Customer cu ON cu.Id = c.CustomerId
             WHERE c.CaseNumber LIKE @like OR c.Subject LIKE @like OR c.ExternalReference = @Text
             ORDER BY c.CreatedUtc DESC) AS cases
    UNION ALL
    SELECT Kind, Id, Title, Detail
      FROM (SELECT TOP (10) 'customer' AS Kind, cu.Id, cu.Name AS Title, cu.AccountNumber + N', ' + ISNULL(cu.City, N'') AS Detail, 2 AS SortGroup
              FROM care.Customer cu
             WHERE cu.Name LIKE @like OR cu.AccountNumber LIKE @Text + N'%' OR cu.Email LIKE @Text + N'%'
             ORDER BY cu.Name) AS customers;
END
GO

CREATE OR ALTER PROCEDURE care.usp_Stats_SaveDaily
    @Day date,
    @QueueCode varchar(12),
    @OpenCases int,
    @Breached int,
    @Created int,
    @Resolved int,
    @AvgResolveHours float = NULL
AS
BEGIN
    SET NOCOUNT ON;

    MERGE care.DailyQueueStats AS t
    USING (SELECT @Day AS [Day], @QueueCode AS QueueCode) AS s
       ON t.[Day] = s.[Day] AND t.QueueCode = s.QueueCode
    WHEN MATCHED THEN
        UPDATE SET OpenCases = @OpenCases, Breached = @Breached, Created = @Created, Resolved = @Resolved, AvgResolveHours = @AvgResolveHours
    WHEN NOT MATCHED THEN
        INSERT ([Day], QueueCode, OpenCases, Breached, Created, Resolved, AvgResolveHours)
        VALUES (@Day, @QueueCode, @OpenCases, @Breached, @Created, @Resolved, @AvgResolveHours);
END
GO

CREATE OR ALTER PROCEDURE care.usp_Customer_MergeStaging
    @BatchId    uniqueidentifier,
    @SourceFile nvarchar(260)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @actions TABLE (Action nvarchar(10));
    DECLARE @total int = (SELECT COUNT(*) FROM care.CustomerImportStaging WHERE BatchId = @BatchId);

    BEGIN TRANSACTION;

    MERGE care.Customer WITH (HOLDLOCK) AS t
    USING (SELECT * FROM care.CustomerImportStaging WHERE BatchId = @BatchId) AS s
       ON t.AccountNumber = s.AccountNumber
    WHEN MATCHED AND (ISNULL(t.Name, N'') <> ISNULL(s.Name, N'') OR ISNULL(t.Email, N'') <> ISNULL(s.Email, N'')
                      OR ISNULL(t.Phone, '') <> ISNULL(s.Phone, '') OR ISNULL(t.Street, N'') <> ISNULL(s.Street, N'')
                      OR ISNULL(t.PostalCode, '') <> ISNULL(s.PostalCode, '') OR ISNULL(t.City, N'') <> ISNULL(s.City, N'')
                      OR t.Segment <> s.Segment OR t.IsPriorityCustomer <> s.IsPriorityCustomer) THEN
        UPDATE SET Name = s.Name, Email = s.Email, Phone = s.Phone, Street = s.Street, PostalCode = s.PostalCode,
                   City = s.City, Segment = s.Segment, IsPriorityCustomer = s.IsPriorityCustomer, ModifiedUtc = SYSUTCDATETIME()
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (AccountNumber, Name, Email, Phone, Street, PostalCode, City, Segment, IsPriorityCustomer)
        VALUES (s.AccountNumber, s.Name, s.Email, s.Phone, s.Street, s.PostalCode, s.City, s.Segment, s.IsPriorityCustomer)
    OUTPUT $action INTO @actions;

    DELETE care.CustomerImportStaging WHERE BatchId = @BatchId;

    DECLARE @inserted int = (SELECT COUNT(*) FROM @actions WHERE Action = 'INSERT');
    DECLARE @updated int = (SELECT COUNT(*) FROM @actions WHERE Action = 'UPDATE');

    INSERT care.CustomerImportLog (SourceFile, Inserted, Updated, Unchanged)
    VALUES (@SourceFile, @inserted, @updated, @total - @inserted - @updated);

    COMMIT;

    SELECT @inserted AS Inserted, @updated AS Updated, @total - @inserted - @updated AS Unchanged;
END
GO

CREATE OR ALTER PROCEDURE care.usp_Case_ArchiveCandidates
    @ClosedBeforeUtc datetime2(0),
    @BatchSize int = 200
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@BatchSize) c.Id
      FROM care.[Case] c
     WHERE c.Status = 5 AND c.ClosedUtc < @ClosedBeforeUtc
     ORDER BY c.ClosedUtc;
END
GO

CREATE OR ALTER PROCEDURE care.usp_Case_Purge
    @CaseId int,
    @ArchiveFile nvarchar(400)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    INSERT care.CaseArchive (CaseNumber, CustomerId, ClosedUtc, ArchiveFile)
    SELECT CaseNumber, CustomerId, ClosedUtc, @ArchiveFile FROM care.[Case] WHERE Id = @CaseId AND Status = 5;

    IF @@ROWCOUNT = 1
    BEGIN
        DELETE care.CaseAttachment WHERE CaseId = @CaseId;
        DELETE care.CaseNote WHERE CaseId = @CaseId;
        DELETE care.EscalationLog WHERE CaseId = @CaseId;
        DELETE care.[Case] WHERE Id = @CaseId;
    END

    COMMIT;
END
GO
