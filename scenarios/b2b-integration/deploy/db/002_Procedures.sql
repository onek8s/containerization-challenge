/*
    VanArsdelIntegration database: stored procedures called by VanArsdel.Integration.Data (Enterprise Library
    Data Access Block), the pipeline components and the portals. CREATE OR ALTER needs SQL Server 2016 SP1.
*/
SET NOCOUNT ON;
GO

/* ---------------------------------------------------------------- partner directory */

CREATE OR ALTER PROCEDURE dbo.usp_GetTradingPartners
AS
BEGIN
    SET NOCOUNT ON;
    SELECT PartnerId, Name, RoleId, StandardId, TransportId, InterchangeQualifier, InterchangeId, StandardVersion,
           Gln, ErpNumber, As2Identifier, CurrencyCode, CountryCode, DefaultShipVia, AckRequired, AckTimeoutMinutes,
           CreditCheckThreshold, Active, TestIndicator
    FROM dbo.TradingPartner
    ORDER BY PartnerId;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_GetPartnerXref
AS
BEGIN
    SET NOCOUNT ON;
    SELECT PartnerId, KindId, PartnerValue, InternalValue, ValidFrom, ValidTo
    FROM dbo.PartnerXref
    WHERE ValidTo IS NULL OR ValidTo >= DATEADD(DAY, -1, CAST(SYSUTCDATETIME() AS date))
    ORDER BY PartnerId, KindId, PartnerValue;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_SavePartnerXref
    @PartnerId      nvarchar(20),
    @KindId         tinyint,
    @PartnerValue   nvarchar(50),
    @InternalValue  nvarchar(50),
    @ValidFrom      date = NULL,
    @ValidTo        date = NULL,
    @ChangedBy      nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @PartnerId <> N'*' AND NOT EXISTS (SELECT 1 FROM dbo.TradingPartner WHERE PartnerId = @PartnerId)
    BEGIN
        RAISERROR (N'Unknown trading partner %s.', 16, 1, @PartnerId);
        RETURN;
    END

    DECLARE @old nvarchar(50);

    BEGIN TRANSACTION;

    SELECT @old = InternalValue
    FROM dbo.PartnerXref WITH (UPDLOCK, HOLDLOCK)
    WHERE PartnerId = @PartnerId AND KindId = @KindId AND PartnerValue = @PartnerValue;

    IF @@ROWCOUNT = 0
        INSERT dbo.PartnerXref (PartnerId, KindId, PartnerValue, InternalValue, ValidFrom, ValidTo, ChangedBy, ChangedUtc)
        VALUES (@PartnerId, @KindId, @PartnerValue, @InternalValue, @ValidFrom, @ValidTo, @ChangedBy, SYSUTCDATETIME());
    ELSE
        UPDATE dbo.PartnerXref
        SET InternalValue = @InternalValue, ValidFrom = @ValidFrom, ValidTo = @ValidTo, ChangedBy = @ChangedBy, ChangedUtc = SYSUTCDATETIME()
        WHERE PartnerId = @PartnerId AND KindId = @KindId AND PartnerValue = @PartnerValue;

    INSERT dbo.PartnerChangeLog (PartnerId, Change, ChangedBy)
    VALUES (@PartnerId,
            CONCAT(N'xref ', @KindId, N' ', @PartnerValue, N': ', ISNULL(@old, N'(new)'), N' -> ', @InternalValue),
            @ChangedBy);

    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_DeletePartnerXref
    @PartnerId      nvarchar(20),
    @KindId         tinyint,
    @PartnerValue   nvarchar(50),
    @ChangedBy      nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DELETE dbo.PartnerXref WHERE PartnerId = @PartnerId AND KindId = @KindId AND PartnerValue = @PartnerValue;
    IF @@ROWCOUNT > 0
        INSERT dbo.PartnerChangeLog (PartnerId, Change, ChangedBy)
        VALUES (@PartnerId, CONCAT(N'xref ', @KindId, N' ', @PartnerValue, N' deleted'), @ChangedBy);

    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_SetPartnerActive
    @PartnerId  nvarchar(20),
    @Active     bit,
    @ChangedBy  nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    UPDATE dbo.TradingPartner
    SET Active = @Active, ChangedBy = @ChangedBy, ChangedUtc = SYSUTCDATETIME()
    WHERE PartnerId = @PartnerId AND Active <> @Active;

    IF @@ROWCOUNT > 0
        INSERT dbo.PartnerChangeLog (PartnerId, Change, ChangedBy)
        VALUES (@PartnerId, CASE WHEN @Active = 1 THEN N'activated' ELSE N'deactivated' END, @ChangedBy);

    COMMIT TRANSACTION;
END
GO

/* ---------------------------------------------------------------- interchange register */

CREATE OR ALTER PROCEDURE dbo.usp_RegisterOutboundInterchange
    @PartnerId          nvarchar(20),
    @ControlNumber      nvarchar(14),
    @GroupControlNumber nvarchar(14) = NULL,
    @TransactionType    nvarchar(10),
    @TransactionCount   int,
    @CreatedUtc         datetime2(3),
    @AckDueUtc          datetime2(3) = NULL,
    @FileName           nvarchar(260) = NULL,
    @BodyHash           char(64) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.Interchange (PartnerId, DirectionId, ControlNumber, GroupControlNumber, TransactionType, TransactionCount,
                            CreatedUtc, AckStatusId, AckDueUtc, FileName, BodyHash)
    VALUES (@PartnerId, 2, @ControlNumber, @GroupControlNumber, @TransactionType, @TransactionCount,
            @CreatedUtc, CASE WHEN @AckDueUtc IS NULL THEN 0 ELSE 1 END, @AckDueUtc, @FileName, @BodyHash);

    SELECT CAST(SCOPE_IDENTITY() AS bigint) AS InterchangeKey;
END
GO

/*
    997 AK1/AK102 and CONTRL UCI/UCM carry the group or interchange number of the original; partners strip or
    keep leading zeros as they like, so the match is numeric where both sides are numeric.
*/
CREATE OR ALTER PROCEDURE dbo.usp_ApplyFunctionalAck
    @PartnerId      nvarchar(20),
    @ControlNumber  nvarchar(14),
    @AckStatusId    tinyint,
    @ReceivedUtc    datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @numeric bigint = TRY_CAST(@ControlNumber AS bigint);

    UPDATE dbo.Interchange
    SET AckStatusId = @AckStatusId, AckReceivedUtc = @ReceivedUtc
    WHERE PartnerId = @PartnerId
      AND DirectionId = 2
      AND AckStatusId IN (1, 5)
      AND (   ControlNumber = @ControlNumber
           OR GroupControlNumber = @ControlNumber
           OR (@numeric IS NOT NULL AND (TRY_CAST(ControlNumber AS bigint) = @numeric OR TRY_CAST(GroupControlNumber AS bigint) = @numeric)));

    SELECT @@ROWCOUNT AS Matched;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_GetInterchangesAwaitingAck
AS
BEGIN
    SET NOCOUNT ON;
    SELECT InterchangeKey, PartnerId, DirectionId, ControlNumber, GroupControlNumber, TransactionType, TransactionCount,
           CreatedUtc, AckStatusId, AckReceivedUtc, AckDueUtc, FileName
    FROM dbo.Interchange
    WHERE AckStatusId IN (1, 5)
    ORDER BY AckDueUtc;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_SearchInterchanges
    @PartnerId  nvarchar(20) = NULL,
    @FromUtc    datetime2(3),
    @ToUtc      datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (2000) InterchangeKey, PartnerId, DirectionId, ControlNumber, GroupControlNumber, TransactionType,
           TransactionCount, CreatedUtc, AckStatusId, AckReceivedUtc, AckDueUtc, FileName
    FROM dbo.Interchange
    WHERE CreatedUtc >= @FromUtc AND CreatedUtc < @ToUtc
      AND (@PartnerId IS NULL OR @PartnerId = N'' OR PartnerId = @PartnerId)
    ORDER BY CreatedUtc DESC
    OPTION (RECOMPILE);
END
GO

/*
    Polled by the WCF-SQL receive location rl_Integration_AckOverdue_WCFSQL (XmlPolling): marks interchanges whose
    acknowledgement window has passed as overdue and returns them once, for the mail to integration support.
*/
CREATE OR ALTER PROCEDURE dbo.usp_PollOverdueAcks
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @overdue table (InterchangeKey bigint, PartnerId nvarchar(20), ControlNumber nvarchar(14), TransactionType nvarchar(10),
                            CreatedUtc datetime2(3), AckDueUtc datetime2(3), FileName nvarchar(260));

    UPDATE dbo.Interchange
    SET AckStatusId = 5
    OUTPUT inserted.InterchangeKey, inserted.PartnerId, inserted.ControlNumber, inserted.TransactionType, inserted.CreatedUtc,
           inserted.AckDueUtc, inserted.FileName INTO @overdue
    WHERE AckStatusId = 1 AND AckDueUtc < SYSUTCDATETIME();

    SELECT o.InterchangeKey, o.PartnerId, p.Name AS PartnerName, o.ControlNumber, o.TransactionType, o.CreatedUtc, o.AckDueUtc, o.FileName
    FROM @overdue AS o
    JOIN dbo.TradingPartner AS p ON p.PartnerId = o.PartnerId
    ORDER BY o.PartnerId, o.CreatedUtc
    FOR XML PATH (N'Interchange'), ELEMENTS;
END
GO

/* Duplicate check of the receive pipelines: 1 when the key is new (or older than the window), 0 for a duplicate. */
CREATE OR ALTER PROCEDURE dbo.usp_RegisterReceivedInterchange
    @PartnerId      nvarchar(20),
    @DuplicateKey   nvarchar(100),
    @ReceivedUtc    datetime2(3),
    @WindowMinutes  int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @isNew int = 1;

    BEGIN TRANSACTION;

    UPDATE dbo.ReceivedInterchange WITH (UPDLOCK, HOLDLOCK)
    SET ReceivedUtc = @ReceivedUtc
    WHERE PartnerId = @PartnerId AND DuplicateKey = @DuplicateKey
      AND ReceivedUtc < DATEADD(MINUTE, -@WindowMinutes, @ReceivedUtc);

    IF @@ROWCOUNT = 0
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.ReceivedInterchange WITH (UPDLOCK, HOLDLOCK) WHERE PartnerId = @PartnerId AND DuplicateKey = @DuplicateKey)
            SET @isNew = 0;
        ELSE
            INSERT dbo.ReceivedInterchange (PartnerId, DuplicateKey, ReceivedUtc) VALUES (@PartnerId, @DuplicateKey, @ReceivedUtc);
    END

    COMMIT TRANSACTION;
    SELECT @isNew AS IsNew;
END
GO

/* ISA13 / GS06 / UNB5 counters. Wraps to 1 after MaxValue (999999999 for ISA13). */
CREATE OR ALTER PROCEDURE dbo.usp_NextControlNumber
    @PartnerId  nvarchar(20),
    @Level      nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @next table (Value bigint);

    BEGIN TRANSACTION;

    UPDATE dbo.ControlNumber WITH (UPDLOCK, HOLDLOCK)
    SET LastValue = CASE WHEN LastValue >= MaxValue THEN 1 ELSE LastValue + 1 END
    OUTPUT inserted.LastValue INTO @next
    WHERE PartnerId = @PartnerId AND Level = @Level;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT dbo.ControlNumber (PartnerId, Level, LastValue) VALUES (@PartnerId, @Level, 1);
        INSERT @next VALUES (1);
    END

    COMMIT TRANSACTION;
    SELECT TOP (1) Value AS ControlNumber FROM @next;
END
GO

/* ---------------------------------------------------------------- SLA monitor */

CREATE OR ALTER PROCEDURE dbo.usp_GetSlaDefinitions
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Code, KindId, Description, PartnerId, ThresholdMinutes, ThresholdCount, SeverityId, BusinessHoursOnly, NotifyGroup, Enabled
    FROM dbo.SlaDefinition
    ORDER BY Code;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_SaveAlertState
    @AlertKey       nvarchar(200),
    @FirstRaisedUtc datetime2(3),
    @LastNotifiedUtc datetime2(3),
    @NotifyCount    int,
    @SeverityId     tinyint,
    @Subject        nvarchar(300),
    @NotifyGroup    nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    MERGE dbo.AlertState WITH (HOLDLOCK) AS t
    USING (SELECT @AlertKey AS AlertKey) AS s ON t.AlertKey = s.AlertKey
    WHEN MATCHED THEN
        UPDATE SET FirstRaisedUtc = CASE WHEN t.ClearedUtc IS NULL THEN t.FirstRaisedUtc ELSE @FirstRaisedUtc END,
                   LastNotifiedUtc = @LastNotifiedUtc, NotifyCount = @NotifyCount, SeverityId = @SeverityId,
                   Subject = @Subject, NotifyGroup = @NotifyGroup, ClearedUtc = NULL
    WHEN NOT MATCHED THEN
        INSERT (AlertKey, FirstRaisedUtc, LastNotifiedUtc, NotifyCount, SeverityId, Subject, NotifyGroup)
        VALUES (@AlertKey, @FirstRaisedUtc, @LastNotifiedUtc, @NotifyCount, @SeverityId, @Subject, @NotifyGroup);
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_LogAlert
    @AlertKey   nvarchar(200),
    @SlaCode    nvarchar(30),
    @SeverityId tinyint,
    @PartnerId  nvarchar(20) = NULL,
    @Subject    nvarchar(300),
    @Detail     nvarchar(max) = NULL,
    @RaisedUtc  datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.AlertLog (AlertKey, SlaCode, SeverityId, PartnerId, Subject, Detail, RaisedUtc)
    VALUES (@AlertKey, @SlaCode, @SeverityId, @PartnerId, @Subject, @Detail, @RaisedUtc);
END
GO

/* ---------------------------------------------------------------- operations portal */

CREATE OR ALTER PROCEDURE dbo.usp_LogResubmission
    @OriginalInstanceId uniqueidentifier,
    @OriginalMessageId  uniqueidentifier,
    @ServiceName        nvarchar(256) = NULL,
    @PartnerId          nvarchar(20) = NULL,
    @MessageType        nvarchar(256) = NULL,
    @Reason             nvarchar(500),
    @BodyEdited         bit,
    @ResubmittedBy      nvarchar(100),
    @Target             nvarchar(260),
    @OriginalBody       varbinary(max) = NULL,
    @SubmittedBody      varbinary(max) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @id bigint;

    BEGIN TRANSACTION;

    INSERT dbo.ResubmitLog (OriginalInstanceId, OriginalMessageId, ServiceName, PartnerId, MessageType, Reason, BodyEdited, ResubmittedBy, Target)
    VALUES (@OriginalInstanceId, @OriginalMessageId, @ServiceName, @PartnerId, @MessageType, @Reason, @BodyEdited, @ResubmittedBy, @Target);
    SET @id = SCOPE_IDENTITY();

    IF @OriginalBody IS NOT NULL OR @SubmittedBody IS NOT NULL
        INSERT dbo.ResubmitBody (ResubmitId, OriginalBody, SubmittedBody)
        VALUES (@id, @OriginalBody, CASE WHEN @BodyEdited = 1 THEN @SubmittedBody END);

    COMMIT TRANSACTION;
    SELECT @id AS ResubmitId;
END
GO

/* ---------------------------------------------------------------- tracking portal users */

CREATE OR ALTER PROCEDURE dbo.usp_SavePortalUser
    @UserName           nvarchar(100),
    @DisplayName        nvarchar(100),
    @Email              nvarchar(200),
    @PasswordHash       nvarchar(200),
    @PartnerId          nvarchar(20) = NULL,
    @Roles              nvarchar(200),
    @Locked             bit,
    @MustChangePassword bit,
    @ChangedBy          nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.PortalUser
    SET DisplayName = @DisplayName,
        Email = @Email,
        PasswordChangedUtc = CASE WHEN PasswordHash <> @PasswordHash THEN SYSUTCDATETIME() ELSE PasswordChangedUtc END,
        PasswordHash = @PasswordHash,
        PartnerId = @PartnerId,
        Roles = @Roles,
        Locked = @Locked,
        FailedAttempts = CASE WHEN @Locked = 0 THEN 0 ELSE FailedAttempts END,
        LockedUntilUtc = CASE WHEN @Locked = 0 THEN NULL ELSE LockedUntilUtc END,
        MustChangePassword = @MustChangePassword,
        ChangedBy = @ChangedBy,
        ChangedUtc = SYSUTCDATETIME()
    WHERE UserName = @UserName;

    IF @@ROWCOUNT = 0
        INSERT dbo.PortalUser (UserName, DisplayName, Email, PasswordHash, PartnerId, Roles, Locked, MustChangePassword, ChangedBy)
        VALUES (@UserName, @DisplayName, @Email, @PasswordHash, @PartnerId, @Roles, @Locked, @MustChangePassword, @ChangedBy);
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_RecordPortalLogin
    @UserName       nvarchar(100),
    @Succeeded      bit,
    @FailedAttempts int,
    @LockedUntilUtc datetime2(3) = NULL,
    @AtUtc          datetime2(3),
    @ClientAddress  nvarchar(64) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.PortalUser
    SET FailedAttempts = @FailedAttempts,
        LockedUntilUtc = @LockedUntilUtc,
        LastLoginUtc = CASE WHEN @Succeeded = 1 THEN @AtUtc ELSE LastLoginUtc END
    WHERE UserName = @UserName;

    INSERT dbo.PortalLogin (UserName, Succeeded, AtUtc, ClientAddress)
    VALUES (@UserName, @Succeeded, @AtUtc, @ClientAddress);
END
GO
